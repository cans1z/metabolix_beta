package com.metabolix.metabolix

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.AtomicFile
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import java.io.File
import java.security.KeyStore
import java.security.MessageDigest
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "metabolix/session")
            .setMethodCallHandler { call, result ->
                try {
                    when (call.method) {
                        "read" -> result.success(readSession(sessionFile(call.arguments as String)))
                        "write" -> {
                            val args = call.arguments as List<*>
                            writeSession(sessionFile(args[0] as String), args[1] as String)
                            result.success(null)
                        }
                        "delete" -> {
                            sessionFile(call.arguments as String).delete()
                            result.success(null)
                        }
                        else -> result.notImplemented()
                    }
                } catch (_: Exception) {
                    result.error("SESSION_STORAGE", "Не удалось открыть защищённое хранилище", null)
                }
            }
    }

    private fun sessionFile(server: String): AtomicFile {
        val digest = MessageDigest.getInstance("SHA-256").digest(server.toByteArray())
        val name = digest.joinToString("") { "%02x".format(it) }
        return AtomicFile(File(noBackupFilesDir, "session-$name"))
    }

    private fun sessionKey(): SecretKey {
        val alias = "metabolix.session"
        val store = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (store.getKey(alias, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").run {
            init(KeyGenParameterSpec.Builder(alias,
                KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .build())
            generateKey()
        }
    }

    private fun readSession(file: AtomicFile): String? {
        val payload = try {
            file.readFully()
        } catch (_: java.io.FileNotFoundException) {
            return null
        }
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, sessionKey(), GCMParameterSpec(128, payload, 0, 12))
        return cipher.doFinal(payload, 12, payload.size - 12).toString(Charsets.UTF_8)
    }

    private fun writeSession(file: AtomicFile, value: String) {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, sessionKey())
        val bytes = cipher.iv + cipher.doFinal(value.toByteArray())
        val stream = file.startWrite()
        try {
            stream.write(bytes)
            file.finishWrite(stream)
        } catch (error: Exception) {
            file.failWrite(stream)
            throw error
        }
    }
}
