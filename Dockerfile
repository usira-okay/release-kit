# 使用 .NET 9 SDK 作為建置映像
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 複製專案檔案並還原套件
COPY ["src/ReleaseKit.Console/ReleaseKit.Console.csproj", "ReleaseKit.Console/"]
COPY ["src/ReleaseKit.Application/ReleaseKit.Application.csproj", "ReleaseKit.Application/"]
COPY ["src/ReleaseKit.Domain/ReleaseKit.Domain.csproj", "ReleaseKit.Domain/"]
COPY ["src/ReleaseKit.Infrastructure/ReleaseKit.Infrastructure.csproj", "ReleaseKit.Infrastructure/"]
COPY ["src/ReleaseKit.Common/ReleaseKit.Common.csproj", "ReleaseKit.Common/"]
RUN dotnet restore "ReleaseKit.Console/ReleaseKit.Console.csproj"

# 複製所有原始碼
COPY src/ .

# 建置應用程式
WORKDIR "/src/ReleaseKit.Console"
RUN dotnet build "ReleaseKit.Console.csproj" -c Release -o /app/build

# 發佈應用程式
FROM build AS publish
RUN dotnet publish "ReleaseKit.Console.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 使用 .NET 9 Runtime 作為執行映像
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# 設定進入點
ENTRYPOINT ["dotnet", "ReleaseKit.Console.dll"]
