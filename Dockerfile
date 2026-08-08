FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY backend/ ./backend/
RUN dotnet restore backend/PermutStib.Api/PermutStib.Api.csproj
RUN dotnet publish backend/PermutStib.Api/PermutStib.Api.csproj -c Release -o /app/publish --no-restore
COPY --from=frontend-build /src/frontend/dist/ /app/publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend-build /app/publish/ ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PermutStib.Api.dll"]

