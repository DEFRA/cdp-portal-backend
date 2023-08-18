#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0-bullseye-slim AS build
WORKDIR /src

COPY . .
WORKDIR "/src/Defra.Cdp.Backend.Api"

FROM build AS publish
RUN dotnet publish "Defra.Cdp.Backend.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# unit test and code coverage
# use the label to identity this layer later
LABEL test=true
#RUN dotnet test -c Release --collect:"XPlat Code Coverage" --results-directory /app/testresults "../Defra.Cdp.Backend.Api.UnitTests/Defra.Cdp.Backend.Api.UnitTests.csproj"  

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8085
ENTRYPOINT ["dotnet", "Defra.Cdp.Backend.Api.dll"]
