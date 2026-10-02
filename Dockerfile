FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj src/backend/ShiftScheduling.Web/
RUN dotnet restore src/backend/ShiftScheduling.Web/ShiftScheduling.Web.csproj

COPY src/backend/ShiftScheduling.Web/ src/backend/ShiftScheduling.Web/

WORKDIR /src/src/backend/ShiftScheduling.Web
RUN dotnet publish ShiftScheduling.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ShiftScheduling.Web.dll"]
