# Architecture Decision Record 003
# Title: Per-Service Database

**Date:** 2025-01-01
**Status:** Accepted

## Context
In a microservices architecture, services can either share a single database or each own a private database. 

## Decision
Each microservice owns its **private SQL Server database**. Services never directly query another service's database; they communicate via REST APIs or integration events.

| Service | Database Name |
|---------|--------------|
| Identity | MangaIdentityDB |
| Submission | MangaSubmissionDB |
| Series | MangaSeriesDB |
| Chapter | MangaChapterDB |
| Task | MangaTaskDB |
| QA | MangaQADB |
| Publishing | MangaPublishingDB |
| Ranking | MangaRankingDB |

## Consequences
- **Pro:** True domain isolation — no schema coupling between services
- **Pro:** Independent schema evolution (migrations don't affect other services)
- **Pro:** Each service can tune its own indexing strategy
- **Con:** No foreign keys across services (data consistency via events + eventual consistency)
- **Con:** Cross-service queries require API calls or denormalized data
- **Mitigation:** Denormalize critical fields (e.g., `BugPin.ChapterId` — FIX-10) and use integration events for sync
