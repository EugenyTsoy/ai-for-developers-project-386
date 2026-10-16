# Changelog

## [1.1.0](https://github.com/EugenyTsoy/ai-for-developers-project-386/compare/v1.0.0...v1.1.0) (2026-10-16)


### Features

* add owner event type creation form ([#27](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/27)) ([1cd0028](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/1cd0028bb118af375da66bc427680cf954d337a7))
* **arch:** introduce Domain/Application/Infrastructure boundaries ([db90ce0](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/db90ce076283e30a9f1b83dbc0689a39ee949eb3))
* **bookings:** implement atomic booking creation ([c58692e](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/c58692e351b9378d0a21222177527eff977bd82f))
* **calendar:** configure calendar and deterministic time (TK3) ([85752aa](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/85752aa0168aa40eb362ef1899e27538903698e1))
* deliver call booking MVP ([#35](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/35)) ([d2adb66](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/d2adb66dc8d25bb1ff8fb5303b25d1382bbc6204))
* **docker:** package frontend and API in one container ([84b4531](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/84b4531187a70618d214d81508fa586481549b7c))
* **docker:** package frontend and API in one container ([fedd709](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/fedd70943e069e35c2decdd5263f000fea710b4e))
* **event-types:** implement persisted creation and listing ([#22](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/22)) ([bf1c49e](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/bf1c49e87d67c84b4a54be4cb2d72ba4346c5ddf))
* **frontend:** complete guest booking flow (TK11) ([0f47492](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/0f47492026371725fc54b6a6eacdf1833234ea53))
* **frontend:** implement guest EventType and Slot selection ([#28](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/28)) ([e4362cb](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/e4362cbf4df106f013d711f3d905d0fbe53b6d7e))
* **frontend:** implement owner upcoming bookings list ([#30](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/30)) ([9126be2](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/9126be2d296bf3d7c1bdd0c410dcbabafa44d720))
* implement event type slot availability ([#23](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/23)) ([6bc0589](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/6bc058952bb35d16966d03356632d5c1265c31f7))
* implement upcoming bookings query (TK8) ([787f148](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/787f14856393e8692e944b66760322e6ae10dcca)), closes [#26](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/26)
* **storage:** persist EventType and Booking via shared in-memory store (TK2) ([0c0904b](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/0c0904be4870fcd98240d087dab948975e70956f))
* use fixed 30-minute booking starts and calendar date selection ([0f8a775](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/0f8a775e729111636565b2c62a4c4f316cc78b02))
* фиксированный интервал стартов и выбор даты после Submission Audit ([90bcfc9](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/90bcfc94d43d8cd4464983ed5402429b38a9d85a))


### Bug Fixes

* **api:** normalize controller seam formatting ([9911cc2](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/9911cc297f16f0d3e45aa46dab2c51493dead058))
* **arch:** narrow TK1 to architectural scaffolding only ([4fb47d8](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/4fb47d80cddf82cc1c4ce22e6b12f306518938f9))
* **arch:** tighten review findings on TK1 ([0bd1dd9](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/0bd1dd9e19b424a202c992b57e7686629550c9ac))
* connect MVP frontend to real API through Vite proxy ([#31](https://github.com/EugenyTsoy/ai-for-developers-project-386/issues/31)) ([4383bfe](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/4383bfe3c795e3e71a058e2a1cb457fd975befd7))
* guard Render MCP mutations and document server environment ([30ecd11](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/30ecd11c206c48d4d9564df25c097b11b7c9853a))
* reject lossy booking timestamps and validate trimmed email ([d13b43c](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/d13b43c0b68a349a47112964eeb319eae44f7d7e))
* reject UTC timestamps with an empty fraction ([8a82cb9](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/8a82cb98175cb667c5123e4b4f06f864005f6a91))

## 1.0.0 (2026-10-09)


### Features

* implement Calendar home page and navigation ([67ebf2a](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/67ebf2a210cb1da773e9775d9e49797d11a2505a))
* initialize CallBooking application scaffold ([e85f9e9](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/e85f9e9047e808b70b2e68a23fafbb5ae622f89f))
* initialize CallBooking application scaffold ([1681c3a](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/1681c3ad6cd89ae3521ac11cb00c7557feda7cf3))


### Bug Fixes

* rename release-please workflow file ([0345876](https://github.com/EugenyTsoy/ai-for-developers-project-386/commit/03458766bc56c143d1df43aba8997c7a64b92ded))
