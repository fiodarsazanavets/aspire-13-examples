# .NET Aspire Examples

Example projects of .NET Aspire.

The majority of these samples are used by the book [.NET Aspire Made Easy](https://www.manning.com/books/dotnet-aspire-made-easy/)

![](manning-aspire-cover.png)

This repository follows the **golden thread** pattern, where we go from the initial Aspire starter project to building an e-commerce system by gradually adding various infrastructure components and business logic. The purpose of this approach is to demonstrate how each integration library for the Aspire infrastructure components works in a realistic scenario.

## List of Examples

1. [The starter application](/BaselineApp)
2. [Docker/Keycloak integration](/AppWithKeycloakAuth)
3. SQL-based database integrations
   - [SQL Server integration](/AppWithSqlServer)
   - [SQL Server with EF Core](/AppWithSqlServerEf)
   - [Oracle DB integration](/AppWithOracleDb)
   - [PostgreSQL integration](/AppWithPostgres)
   - [PostgreSQL with EF Core](/AppWithPostgresEf/)
4. [MongoDB integration](/AppWithMongoDb)
5. Azure Storage integrations
   - [Azure Table Storage](/AppWithAzureTableStorage)
   - [Azure Blob Storage](/AppWithAzureBlobStorage)
   - [Azure Queue Storage](/AppWithAzureQueueStorage)
6. [SignalR integration](/AppWithDeliveryTrackingSignalR)
7. [Semantic Kernel and Ollama](/AppWithOllamaChatbot)
8. [Integration tests](/AppWithTests)
9. [RabbitMQ integration](/AppWithRabbitMq)
10. [Redis caching and locking](/AppWithRedisCacheAndLock)
11. [Full authentication integration](/AppWithAuthentication)
12. [Scale-out and deployment scripts](/AppWithInfrastructure)
13. [Aspire version upgrade flow](/AspireVersionUpgrade)
   - [.NET Aspire 8 example](/AspireVersionUpgrade/Aspire8App)
   - [.NET Aspire 9 example](/AspireVersionUpgrade/Aspire9App)
   - [Aspire 13 example](/AspireVersionUpgrade/Aspire13App)
14. [Aspire orchestrator enrollment](/AspireOrchestratorEnrollment)
   - [Pre-enrollment setup](/AspireOrchestratorEnrollment/PreEnrollmentApp)
   - [Post-enrollment setup](/AspireOrchestratorEnrollment/PostEnrollmentApp)