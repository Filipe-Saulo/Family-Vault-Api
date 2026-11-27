# Imagem da API. Duas saídas do mesmo build:
# - a API (ENTRYPOINT), que nunca aplica migration ao subir;
# - /app/efbundle, o executável das migrations, rodado como passo separado do deploy (um Container Apps Job com esta
#   mesma imagem e o comando /app/efbundle), com a connection string em ConnectionStrings__DefaultConnection.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
# Só o csproj primeiro: a camada do restore fica em cache enquanto as dependências não mudam
COPY FamilyVaultApi/FamilyVaultApi.csproj FamilyVaultApi/
RUN dotnet restore FamilyVaultApi/FamilyVaultApi.csproj
COPY FamilyVaultApi/ FamilyVaultApi/
RUN dotnet publish FamilyVaultApi/FamilyVaultApi.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM build AS migrations
# Mesma versão do EF Core do projeto
RUN dotnet tool install --global dotnet-ef --version 8.0.10
ENV PATH="${PATH}:/root/.dotnet/tools"
# O bundle cria o DbContext pela DesignTimeDbContextFactory: não precisa de JwtSettings, Twilio nem Resend
RUN dotnet ef migrations bundle --project FamilyVaultApi/FamilyVaultApi.csproj --configuration Release --output /app/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=migrations /app/efbundle ./efbundle
# O Serilog também grava em ./logs; o resto de /app fica só leitura para o usuário da aplicação
RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs
# Porta sem privilégio (o ingress do Container Apps aponta para ela) e o ForwardedHeaders ligado: a API sempre roda
# atrás de proxy. O ForwardedHeaders__ForwardLimit sai do ambiente do Container App.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ForwardedHeaders__Enabled=true
EXPOSE 8080
# Usuário sem privilégio que já vem na imagem do .NET 8 (UID 1654)
USER $APP_UID
ENTRYPOINT ["dotnet", "FamilyVaultApi.dll"]
