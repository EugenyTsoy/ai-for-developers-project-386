FROM node:24-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY src/frontend/callbooking.web/package*.json ./
RUN npm ci
COPY src/frontend/callbooking.web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY src/backend/CallBooking.Api/CallBooking.Api.csproj src/backend/CallBooking.Api/
COPY src/backend/CallBooking.Application/CallBooking.Application.csproj src/backend/CallBooking.Application/
COPY src/backend/CallBooking.Domain/CallBooking.Domain.csproj src/backend/CallBooking.Domain/
COPY src/backend/CallBooking.Infrastructure/CallBooking.Infrastructure.csproj src/backend/CallBooking.Infrastructure/
RUN dotnet restore src/backend/CallBooking.Api/CallBooking.Api.csproj
COPY src/backend/ src/backend/
RUN dotnet publish src/backend/CallBooking.Api/CallBooking.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /src/frontend/dist ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production \
    PORT=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CallBooking.Api.dll"]
