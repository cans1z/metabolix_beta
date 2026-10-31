# Сборка идёт в образе с SDK, а в итоговый образ попадает только готовое приложение: так он в разы меньше.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Metabolix.Api/Metabolix.Api.csproj Metabolix.Api/
RUN dotnet restore Metabolix.Api/Metabolix.Api.csproj
COPY Metabolix.Api/ Metabolix.Api/
RUN dotnet publish Metabolix.Api/Metabolix.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
# Сервер работает не от root; папка картинок должна принадлежать этому пользователю.
RUN mkdir -p /app/uploads && chown -R $APP_UID /app/uploads
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Metabolix.Api.dll"]
