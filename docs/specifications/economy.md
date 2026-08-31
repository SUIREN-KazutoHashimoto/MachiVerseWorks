# Economy and Employment

Phase 21 introduces deterministic economic actors and connects them to the existing population and transport simulation.

## Model

- `Company` owns one or more establishments and tracks cash, revenue, expense, production capacity, and cumulative production.
- `Establishment` references an existing Building and/or POI; no second geography model is introduced.
- `Job` belongs to an establishment, has a required worker count and daily wage, and exposes filled/vacant positions.
- `Employment` maps a Person to a Job. During work hours its establishment becomes the Person's work destination, so the existing walking, private-vehicle, and multimodal transit planners handle commuting.
- Household economy state tracks cash balance, cumulative income, and cumulative spending.

Money is represented as signed 64-bit integer values in the application's smallest currency unit. Production quantities use finite non-negative `double` values. Economic processing is ordered by stable IDs so checkpoint restore produces deterministic continuation.

## Economic cycle

An economic day performs production, wage payment, then household consumption. Production is scaled by filled positions. Wages move cash from Company to Household and update Company expense / Household income. Consumption moves cash from Household to a deterministic commercial establishment's Company and updates Household spending / Company revenue.

## Persistence

Save format v11 adds the Economy checkpoint. v10 and older supported saves migrate with an empty Economy state. Company, Establishment, Job, Employment, and Household economy collections are included in Save Data limit validation.

## Protocol and Web

Protocol 2.10 adds `EconomySnapshot` (`MessageType` 730). The server publishes aggregate statistics plus capped Company/Household debug entries only to clients that negotiated 2.10 or newer. The Web client decodes the same binary layout and displays an Economy debug summary.

## Verification

Phase 21 includes deterministic Simulation tests, Save v11 migration/round-trip tests, Protocol codec tests, a Server-to-Browser Economy E2E path, and BenchmarkDotNet coverage for Economy statistics/snapshot creation.
