import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:math';

import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

typedef Json = Map<String, dynamic>;

class ApiException implements Exception {
  final String message;
  final int? status;
  const ApiException(this.message, [this.status]);

  @override
  String toString() => message;
}

class ApiClient {
  static const _address = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:8080',
  );
  static const _storage = MethodChannel('metabolix/session');
  static const _timeout = Duration(seconds: 12);
  final _base = Uri.parse(_address);
  final _http = HttpClient()..connectionTimeout = _timeout;
  String? _access;
  String? _refresh;
  DateTime? _expiresAt;
  Future<void>? _refreshing;

  bool get hasSession => _refresh != null;

  Future<void> restore() async {
    final saved = await _storage.invokeMethod<String>('read', _address);
    if (saved == null) {
      _access = _refresh = null;
      _expiresAt = null;
      return;
    }
    final session = jsonDecode(saved) as Json;
    _setTokens(session['accessToken'] as String, session['refreshToken'] as String);
  }

  Future<void> saveSession(Json response) async {
    final access = response['accessToken'] as String;
    final refresh = response['refreshToken'] as String;
    await _storage.invokeMethod<void>('write', [
      _address,
      jsonEncode({'accessToken': access, 'refreshToken': refresh}),
    ]);
    _setTokens(access, refresh);
  }

  void _setTokens(String access, String refresh) {
    final payload = access.split('.')[1];
    final claims = jsonDecode(utf8.decode(base64Url.decode(base64Url.normalize(payload)))) as Json;
    _expiresAt = DateTime.fromMillisecondsSinceEpoch((claims['exp'] as int) * 1000, isUtc: true);
    _access = access;
    _refresh = refresh;
  }

  Future<void> clearSession() async {
    await _storage.invokeMethod<void>('delete', _address);
    _access = _refresh = null;
    _expiresAt = null;
  }

  Future<void> logout() async {
    final pending = _refreshing;
    if (pending != null) {
      try {
        await pending;
      } on ApiException {
        // Даже без сети завершаем локальную сессию.
      }
    }
    final token = _refresh;
    await clearSession();
    if (token != null) {
      await request('POST', 'auth/logout', body: {'refreshToken': token}, auth: false);
    }
  }

  Future<void> _renew() async {
    final token = _refresh;
    if (token == null) throw const ApiException('Войдите заново', 401);
    try {
      final response = await request(
        'POST',
        'auth/refresh',
        body: {'refreshToken': token},
        auth: false,
      );
      await saveSession(response as Json);
    } on ApiException catch (e) {
      if (e.status == 401) await clearSession();
      rethrow;
    }
  }

  Future<void> _refreshAccess() async {
    final refresh = _refreshing ??= _renew();
    try {
      await refresh;
    } finally {
      if (identical(_refreshing, refresh)) _refreshing = null;
    }
  }

  Future<dynamic> request(
    String method,
    String path, {
    Json? body,
    Map<String, String>? query,
    bool auth = true,
    bool text = false,
  }) async {
    if (auth && _expiresAt?.isBefore(DateTime.now()) == true) await _refreshAccess();
    final access = auth ? _access : null;
    try {
      return await _send(method, path, body, query, access, text);
    } on ApiException catch (e) {
      if (e.status != 401 || access == null || !hasSession) rethrow;
      if (_refreshing != null || _access == access) await _refreshAccess();
      if (!hasSession) rethrow;
      return _send(method, path, body, query, _access, text);
    }
  }

  Future<dynamic> _send(
    String method,
    String path,
    Json? body,
    Map<String, String>? query,
    String? token,
    bool text,
  ) async {
    if (_base.host.isEmpty || (_base.scheme != 'https' && !(kDebugMode && _base.scheme == 'http'))) {
      throw const ApiException('Укажите HTTPS-адрес API через API_BASE_URL');
    }
    final prefix = _base.path.replaceFirst(RegExp(r'/+$'), '');
    final url = _base.replace(path: '$prefix/api/$path', queryParameters: query);
    try {
      final request = await _http.openUrl(method, url).timeout(_timeout);
      request.followRedirects = false;
      if (token != null) request.headers.set(HttpHeaders.authorizationHeader, 'Bearer $token');
      if (body != null) {
        request.headers.contentType = ContentType.json;
        request.add(utf8.encode(jsonEncode(body)));
      }
      final response = await request.close().timeout(_timeout, onTimeout: () {
        request.abort();
        throw TimeoutException('API');
      });
      final raw = await utf8.decoder.bind(response).join().timeout(_timeout);
      if (response.statusCode < 200 || response.statusCode >= 300) {
        Json? problem;
        try {
          final decoded = jsonDecode(raw);
          if (decoded is Json) problem = decoded;
        } on FormatException {
          // Nginx может вернуть HTML, а сервер — пустой ответ.
        }
        final errors = problem?['errors'];
        final validation = errors is Map ? errors.values.expand((v) => v as List).join('\n') : null;
        throw ApiException(
          validation ??
              (problem?['detail'] as String?) ??
              (response.statusCode == 401 ? 'Войдите заново' : 'Ошибка сервера (${response.statusCode})'),
          response.statusCode,
        );
      }
      return text ? raw : (raw.isEmpty ? null : jsonDecode(raw));
    } on TimeoutException {
      throw const ApiException('Сервер не ответил вовремя. Проверьте соединение');
    } on IOException {
      throw const ApiException('Сервер недоступен. Проверьте соединение и адрес API');
    } on FormatException {
      throw const ApiException('Сервер вернул некорректный ответ');
    }
  }
}

final _random = Random.secure();

String entryUuid() {
  final bytes = List.generate(16, (_) => _random.nextInt(256));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
  return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-${hex.substring(12, 16)}-'
      '${hex.substring(16, 20)}-${hex.substring(20)}';
}
