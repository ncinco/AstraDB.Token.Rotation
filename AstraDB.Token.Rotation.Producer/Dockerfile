#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["AstraDB.Token.Rotation.Producer/AstraDB.Token.Rotation.Producer.csproj", "AstraDB.Token.Rotation.Producer/"]
COPY ["AstraDB.Token.Rotation.Configuration/AstraDB.Token.Rotation.Configuration.csproj", "AstraDB.Token.Rotation.Configuration/"]
COPY ["AstraDB.Token.Rotation.Models/AstraDB.Token.Rotation.Models.csproj", "AstraDB.Token.Rotation.Models/"]
COPY ["AstraDB.Token.Rotation.Services/AstraDB.Token.Rotation.Services.csproj", "AstraDB.Token.Rotation.Services/"]
RUN dotnet restore "AstraDB.Token.Rotation.Producer/AstraDB.Token.Rotation.Producer.csproj"
COPY . .
WORKDIR "/src/AstraDB.Token.Rotation.Producer"
RUN dotnet build "AstraDB.Token.Rotation.Producer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AstraDB.Token.Rotation.Producer.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AstraDB.Token.Rotation.Producer.dll"]