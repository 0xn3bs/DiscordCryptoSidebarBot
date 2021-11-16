# Discord Crypto Sidebar Bot

## Pre-requisites:
    .NET 6 see Step 2 [here](https://docs.microsoft.com/en-us/dotnet/iot/deployment).

## How to use
### Build the project first using

    dotnet build

### Run using:

    dotnet run --no-build --updateInterval 30 --apiId [coin api id] --botToken [bot token here]

### or with PM2:

    pm2 start "dotnet run --no-build --updateInterval 30 --apiId [coin api id] --botToken [bot token here]" --name eth

## How to update:
### Standalone:

    git pull && dotnet build

### or with PM2:

    git pull && pm2 stop all && dotnet build && pm2 restart all
