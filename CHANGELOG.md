# Changelog

## [1.3.2](https://github.com/Bhahlou/RaidOps.Service/compare/v1.3.1...v1.3.2) (2026-07-25)


### 🐛 Bug Fixes

* Require EmbedLinks permission for a notification channel to be marked postable ([#54](https://github.com/Bhahlou/RaidOps.Service/issues/54)) ([8c2bee0](https://github.com/Bhahlou/RaidOps.Service/commit/8c2bee06ba8178a4549e30287d130fec03efa6a1))

## [1.3.1](https://github.com/Bhahlou/RaidOps.Service/compare/v1.3.0...v1.3.1) (2026-07-25)


### 🐛 Bug Fixes

* Stop using non-invariant CultureInfo for absence notification date formatting ([#51](https://github.com/Bhahlou/RaidOps.Service/issues/51)) ([423899c](https://github.com/Bhahlou/RaidOps.Service/commit/423899c727f94531d2d2209f15ccbffe13f3cc21))

## [1.3.0](https://github.com/Bhahlou/RaidOps.Service/compare/v1.2.1...v1.3.0) (2026-07-25)


### 🚀 Features

* Add guild Discord notification settings and switch availability announcements to real state diffing ([#48](https://github.com/Bhahlou/RaidOps.Service/issues/48)) ([c8ef113](https://github.com/Bhahlou/RaidOps.Service/commit/c8ef113ba54d42bf99f9c32b4b99bde537427c28))

## [1.2.1](https://github.com/Bhahlou/RaidOps.Service/compare/v1.2.0...v1.2.1) (2026-07-22)


### 🐛 Bug Fixes

* Partial availability now requires at least one time bound ([#45](https://github.com/Bhahlou/RaidOps.Service/issues/45)) ([11d0998](https://github.com/Bhahlou/RaidOps.Service/commit/11d09985efe645e74294b9feec2b82aaadd1ec67))

## [1.2.0](https://github.com/Bhahlou/RaidOps.Service/compare/v1.1.0...v1.2.0) (2026-07-20)


### 🚀 Features

* Add guild calendar availability declarations (one-off exceptions and recurring patterns) ([#42](https://github.com/Bhahlou/RaidOps.Service/issues/42)) ([d2a3c5e](https://github.com/Bhahlou/RaidOps.Service/commit/d2a3c5e73f4d6408c2eac6d05a32b3c3aec74321))

## [1.1.0](https://github.com/Bhahlou/RaidOps.Service/compare/v1.0.0...v1.1.0) (2026-07-19)


### 🚀 Features

* support linking multiple Battle.net accounts per user ([#40](https://github.com/Bhahlou/RaidOps.Service/issues/40)) ([f31878f](https://github.com/Bhahlou/RaidOps.Service/commit/f31878f2e90d9daf0bb14fb46b7004c3dda30695))


### 🐛 Bug Fixes

* close JWT token-type confusion and CSRF gaps, bump Microsoft.Ope… ([#37](https://github.com/Bhahlou/RaidOps.Service/issues/37)) ([988a1e9](https://github.com/Bhahlou/RaidOps.Service/commit/988a1e942999e46626fd7b79dcdf260df6178662))
* prevent excluding a guild member with an equal or higher role ([#39](https://github.com/Bhahlou/RaidOps.Service/issues/39)) ([6b087e9](https://github.com/Bhahlou/RaidOps.Service/commit/6b087e9038e82e6edd2d64b79eb64a3a29083629))

## [1.0.0](https://github.com/Bhahlou/RaidOps.Service/compare/v0.2.1...v1.0.0) (2026-07-06)


### 🚀 Features

* add per-guild Officer access threshold and in-app notification … ([#32](https://github.com/Bhahlou/RaidOps.Service/issues/32)) ([432d0ef](https://github.com/Bhahlou/RaidOps.Service/commit/432d0ef13a3de3805b5fd90a1388c9625b4b6857))
* Expose bulk eligible guilds ([#30](https://github.com/Bhahlou/RaidOps.Service/issues/30)) ([bf8c5c2](https://github.com/Bhahlou/RaidOps.Service/commit/bf8c5c2480afdb168dc197be5e7cf2faad4ac164))
* Implement character viable specs selection ([#26](https://github.com/Bhahlou/RaidOps.Service/issues/26)) ([4852b1d](https://github.com/Bhahlou/RaidOps.Service/commit/4852b1d41084be8cc42c347f841d7e0bd4f4ed83))
* implement get started ([#28](https://github.com/Bhahlou/RaidOps.Service/issues/28)) ([f5e5e39](https://github.com/Bhahlou/RaidOps.Service/commit/f5e5e396c5f1a7d23b2892c7750bf7dd1b2aad96))
* implement guild action log and guild membership ([#25](https://github.com/Bhahlou/RaidOps.Service/issues/25)) ([1f2f642](https://github.com/Bhahlou/RaidOps.Service/commit/1f2f64236ceef1d3b4fa1bd06bf4c36bfe89d63f))
* Implement guild audit log viewer ([8218402](https://github.com/Bhahlou/RaidOps.Service/commit/8218402bee10c7d7bd294496b5baa1c4855fd9b5))
* implement guild onboarding ([#23](https://github.com/Bhahlou/RaidOps.Service/issues/23)) ([edf5fac](https://github.com/Bhahlou/RaidOps.Service/commit/edf5fac42bbe1a8026c860696841b3397b3522e2))
* implement roster list ([#31](https://github.com/Bhahlou/RaidOps.Service/issues/31)) ([5c89262](https://github.com/Bhahlou/RaidOps.Service/commit/5c89262cf5bd3eff018201fae78c91b6c44ce921))


### 🐛 Bug Fixes

* repair spec icons ([#29](https://github.com/Bhahlou/RaidOps.Service/issues/29)) ([1519bf9](https://github.com/Bhahlou/RaidOps.Service/commit/1519bf98df5c9aaa1601fe1fd7db351220db4900))


### 🧹 Chores

* mark next release as 1.0.0 ([#34](https://github.com/Bhahlou/RaidOps.Service/issues/34)) ([2b0f07a](https://github.com/Bhahlou/RaidOps.Service/commit/2b0f07a3d70282a5cd5bd47472019b281754d04a))

## [0.2.1](https://github.com/Bhahlou/RaidOps.Service/compare/v0.2.0...v0.2.1) (2026-06-06)


### 🐛 Bug Fixes

* Indicate ongoing roll out to host ([#20](https://github.com/Bhahlou/RaidOps.Service/issues/20)) ([43e3d6b](https://github.com/Bhahlou/RaidOps.Service/commit/43e3d6bd79d57c2a48a9678e5b8a3bf6df240e40))

## [0.2.0](https://github.com/Bhahlou/RaidOps.Service/compare/v0.1.0...v0.2.0) (2026-06-06)


### 🚀 Features

* Deploy release pipeline ([#13](https://github.com/Bhahlou/RaidOps.Service/issues/13)) ([4f9ec33](https://github.com/Bhahlou/RaidOps.Service/commit/4f9ec33ee4c7202dd50be561a79969bb5a13a267))
* Enhance discord notification on new release ([#16](https://github.com/Bhahlou/RaidOps.Service/issues/16)) ([2f3120d](https://github.com/Bhahlou/RaidOps.Service/commit/2f3120dfa5bd6d2a2470c4d6fe3f06757b6d095d))


### 🐛 Bug Fixes

* Exclude migrations from CI sonar cloud analysis and fix code smell ([#11](https://github.com/Bhahlou/RaidOps.Service/issues/11)) ([f4c6dd7](https://github.com/Bhahlou/RaidOps.Service/commit/f4c6dd79f7fa2dd52e485b1542be79dbf4a73d6f))
* Fix again again repo name in discord url ([#19](https://github.com/Bhahlou/RaidOps.Service/issues/19)) ([b4ea838](https://github.com/Bhahlou/RaidOps.Service/commit/b4ea838ca6c53903b1af1feb1e95f08bb160f38e))
* Fix incorrect repo url on discord notification ([#17](https://github.com/Bhahlou/RaidOps.Service/issues/17)) ([dd6482a](https://github.com/Bhahlou/RaidOps.Service/commit/dd6482a510c13cf3d8cdb0cffe00cc64ad9f9187))
* Fix incorrect url again ... ([#18](https://github.com/Bhahlou/RaidOps.Service/issues/18)) ([58f0f8f](https://github.com/Bhahlou/RaidOps.Service/commit/58f0f8f13fc2bdcdbf98d8e38961f17eeff2876f))
