FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["BankTransferService/BankTransferService.csproj", "BankTransferService/"]
RUN dotnet restore "BankTransferService/BankTransferService.csproj"

COPY . .
WORKDIR /src/BankTransferService
RUN dotnet publish "BankTransferService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BankTransferService.dll"]
