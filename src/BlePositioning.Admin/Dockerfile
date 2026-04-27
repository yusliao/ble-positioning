FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /workspace
# Admin 客户展示：csproj 在 publish 时从 ../../doc 与 ../../README.md 复制 Markdown，需与本地仓库布局一致
COPY src ./src
COPY doc ./doc
COPY README.md ./README.md
RUN dotnet publish src/BlePositioning.Admin/BlePositioning.Admin.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BlePositioning.Admin.dll"]
