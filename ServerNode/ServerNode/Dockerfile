FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["ServerNode/ServerNode.csproj", "ServerNode/"]
RUN dotnet restore "ServerNode/ServerNode.csproj"
COPY . .
WORKDIR "/src/ServerNode"
RUN dotnet build "ServerNode.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ServerNode.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ServerNode.dll"]
