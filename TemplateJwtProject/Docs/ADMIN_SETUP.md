# Quick Start Guide - Eerste Admin Aanmaken

## Methode 1: Via Program.cs (Aanbevolen voor development)

Voeg deze code toe aan `Program.cs` na de `RoleInitializer`:

```csharp
// Initialiseer rollen
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await RoleInitializer.InitializeAsync(services);
    
    // Maak eerste admin aan
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var adminEmail = "admin@example.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };
        
        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, Roles.Admin);
            Console.WriteLine($"? Admin account created: {adminEmail}");
        }
    }
}
```

**Admin inloggegevens:**
- Email: `admin@example.com`
- Password: `Admin123!`

## Methode 2: Via API Calls (alleen met bestaande admin token)

Gebruik deze methode om **extra** admins aan te maken nadat er al minstens één admin bestaat.

### Stap 1: Registreer een nieuwe admin
```bash
POST /api/auth/register
Authorization: Bearer {admin-token}
{
  "email": "admin@example.com",
  "password": "Admin123!",
  "confirmPassword": "Admin123!"
}
```
Deze endpoint wijst automatisch de Admin rol toe.

## Methode 3: Via EF Core Migration Data Seeding

Maak een nieuwe migratie met seed data:

```csharp
// In een nieuwe migration file
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Hash het wachtwoord
    var hasher = new PasswordHasher<ApplicationUser>();
    var adminId = Guid.NewGuid().ToString();
    
    migrationBuilder.InsertData(
        table: "AspNetUsers",
        columns: new[] { "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", 
                        "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
                        "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount" },
        values: new object[] { 
            adminId, 
            "admin@example.com", 
            "ADMIN@EXAMPLE.COM", 
            "admin@example.com", 
            "ADMIN@EXAMPLE.COM",
            true,
            hasher.HashPassword(null, "Admin123!"),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            false,
            false,
            false,
            0
        }
    );
}
```

## Verificatie

Test of de admin account correct is aangemaakt:

```bash
# Login als admin
curl -X POST https://localhost:7xxx/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}'
```

**Verwachte response:**
```json
{
  "token": "eyJhbG...",
  "email": "admin@example.com",
  "roles": ["Admin"],
  "expiresAt": "..."
}
```

Controleer dat de response **de Admin rol** bevat: `["Admin"]`

## Test Admin Endpoints

```bash
# Gebruik het token van hierboven
curl -X GET https://localhost:7xxx/api/admin/admins \
  -H "Authorization: Bearer YOUR_ADMIN_TOKEN"
```

## Troubleshooting

### "Unauthorized" bij admin endpoints
- Controleer of de token de "Admin" rol bevat (decode op jwt.io)
- Controleer in de database of de user de Admin rol heeft:
  ```sql
  SELECT u.Email, r.Name 
  FROM AspNetUsers u
  JOIN AspNetUserRoles ur ON u.Id = ur.UserId
  JOIN AspNetRoles r ON ur.RoleId = r.Id
  WHERE u.Email = 'admin@example.com';
  ```

### Rollen zijn niet aangemaakt
- Controleer `RoleInitializer` in Program.cs
- Controleer AspNetRoles tabel:
  ```sql
  SELECT * FROM AspNetRoles;
  ```
  Moet bevatten: Admin

## Security Note

?? **BELANGRIJK**: 
- Verwijder de admin-creatie code uit `Program.cs` in productie!
- Wijzig het standaard admin wachtwoord direct na eerste login
- Gebruik sterke wachtwoorden in productie
