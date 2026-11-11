# собираем с sdk и переносим готовое приложение в runtime
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Metabolix.Api/Metabolix.Api.csproj Metabolix.Api/
RUN dotnet restore Metabolix.Api/Metabolix.Api.csproj
COPY Metabolix.Api/ Metabolix.Api/
RUN dotnet publish Metabolix.Api/Metabolix.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
# папка загрузок принадлежит пользователю приложения
RUN mkdir -p /app/uploads && chown -R $APP_UID /app/uploads
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Metabolix.Api.dll"]
