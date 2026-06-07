# Architecture Decision Record 002
# Title: RabbitMQ over Kafka

**Date:** 2025-01-01
**Status:** Accepted

## Context
The system needs reliable async messaging for integration events (e.g., `SubmissionApprovedEvent`, `ChapterApprovedEvent`). Options considered: Apache Kafka, RabbitMQ.

## Decision
Use **RabbitMQ** via **MassTransit** abstraction layer.

## Reasoning
- **Throughput requirements:** MangaERP volume doesn't require Kafka's partitioned log design
- **Operational simplicity:** RabbitMQ is easier to run and manage for the team size
- **MassTransit:** Provides a clean abstraction — switching to Azure Service Bus or Kafka later requires only config changes, not code rewrites
- **Message patterns:** Need request/response and pub/sub, both well-supported by RabbitMQ

## Consequences
- **Pro:** Simpler infrastructure setup
- **Pro:** First-class MassTransit support with saga/outbox patterns
- **Con:** Lower throughput ceiling than Kafka (acceptable for projected load)
