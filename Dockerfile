FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["DMD.Marketing/DMD.Marketing.csproj", "DMD.Marketing/"]
RUN dotnet restore "DMD.Marketing/DMD.Marketing.csproj"
COPY . .
WORKDIR "/src/DMD.Marketing"
RUN dotnet build "DMD.Marketing.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DMD.Marketing.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet DMD.Marketing.dll"]
