# Identity persistence

Identity owns `IdentityDb` and its EF Core migrations. The service applies pending migrations during startup; `EnsureCreated` is not used for the application database.

Add future migrations from this service project as the identity model changes.
