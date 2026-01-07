export PATH="$HOME/.dotnet:$PATH" && dotnet clean
export PATH="$HOME/.dotnet:$PATH" && dotnet publish -c Release -r win-x64 --self-contained -o ./publish