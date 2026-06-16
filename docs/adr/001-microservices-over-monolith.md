# Architecture Decision Record 001
# Title: Microservices over Monolith

**Date:** 2025-01-01
**Status:** Accepted

## Context
The Manga Production Platform needs to support multiple independent workflows (MF1, MF2, MF3, MF5, MF8) with separate teams potentially owning different domains. A monolithic architecture would tightly couple these concerns.

## Decision
Adopt a microservices architecture with per-service databases, each exposing REST APIs behind a single YARP API Gateway.

## Consequences
- **Pro:** Independent deployability and scalability per service
- **Pro:** Teams can own domains independently
- **Pro:** Fault isolation: a failing QA service won't bring down submission
- **Con:** Increased operational complexity (networking, service discovery)
- **Con:** Distributed transaction management required (compensating transactions / sagas)
- **Mitigation:** Use MassTransit + RabbitMQ for reliable async messaging between services
