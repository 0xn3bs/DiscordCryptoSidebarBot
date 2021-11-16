# Discord Crypto Sidebar Bot

## How to use
### Build the project first using

    dotnet build

### Run using:

    dotnet run --no-build --updateInterval 30 --apiId [coin api id] --botToken [bot token here]

### or with PM2:

    pm2 start "dotnet run --no-build --updateInterval 30 --apiId [coin api id] --botToken [bot token here]" --name eth
