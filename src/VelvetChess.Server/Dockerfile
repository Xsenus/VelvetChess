FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/VelvetChess.Core/VelvetChess.Core.csproj src/VelvetChess.Core/
COPY src/VelvetChess.Server/VelvetChess.Server.csproj src/VelvetChess.Server/
RUN dotnet restore src/VelvetChess.Server/VelvetChess.Server.csproj
COPY src/VelvetChess.Core/ src/VelvetChess.Core/
COPY src/VelvetChess.Server/ src/VelvetChess.Server/
COPY src/VelvetChess.App/Resources/Raw/puzzles.json src/VelvetChess.App/Resources/Raw/puzzles.json
RUN dotnet publish src/VelvetChess.Server/VelvetChess.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "VelvetChess.Server.dll"]
