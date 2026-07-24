FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["NuGet.Config", "./"]
COPY ["Directory.Build.props", "./"]
COPY ["Kpett.ChatApp.csproj", "./"]
RUN dotnet restore "Kpett.ChatApp.csproj" --configfile NuGet.Config --packages /root/.nuget/packages

COPY . .
RUN dotnet publish "Kpett.ChatApp.csproj" \
    --configuration "$BUILD_CONFIGURATION" \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

COPY --from=build /app/publish .
COPY entrypoint.sh /app/entrypoint.sh
RUN mkdir -p /app/Logs && chown -R "$APP_UID":0 /app && chmod 755 /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]
