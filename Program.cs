using Dapper;

DefaultTypeMap.MatchNamesWithUnderscores = true;

const string cs = "Host=localhost;Port=5432;Database=library;Username=lab;Password=lab123";
var repo = new LibraryRepository(cs);


await repo.InitializeSchemaAsync();

var tolkienId = await repo.AddAuthorAsync(new Author { Name = "J.R.R. Tolkien", Country = "UK" });
var hobbitId = await repo.AddBookAsync(new Book { Title = "The Hobbit", Year = 1937, AuthorId = tolkienId });
var lotrId = await repo.AddBookAsync(new Book { Title = "The Fellowship of the Ring", Year = 1954, AuthorId = tolkienId });

var orwellId = await repo.AddAuthorAsync(new Author { Name = "George Orwell", Country = "UK" });
var book1984Id = await repo.AddBookAsync(new Book { Title = "1984", Year = 1949, AuthorId = orwellId });
var animalFarmId = await repo.AddBookAsync(new Book { Title = "Animal Farm", Year = 1945, AuthorId = orwellId });

var asimovId = await repo.AddAuthorAsync(new Author { Name = "Isaac Asimov", Country = "USA" });
var foundationId = await repo.AddBookAsync(new Book { Title = "Foundation", Year = 1951, AuthorId = asimovId });


var fantasyId = await repo.AddGenreAsync("Fantasy");
var adventureId = await repo.AddGenreAsync("Adventure");
var dystopiaId = await repo.AddGenreAsync("Dystopia");
var sciFiId = await repo.AddGenreAsync("Sci-Fi");
var satireId = await repo.AddGenreAsync("Political Satire");

await repo.AssignGenreToBookAsync(hobbitId, fantasyId);
await repo.AssignGenreToBookAsync(hobbitId, adventureId);
await repo.AssignGenreToBookAsync(lotrId, fantasyId);
await repo.AssignGenreToBookAsync(lotrId, adventureId);
await repo.AssignGenreToBookAsync(book1984Id, dystopiaId);
await repo.AssignGenreToBookAsync(book1984Id, sciFiId);
await repo.AssignGenreToBookAsync(animalFarmId, dystopiaId);
await repo.AssignGenreToBookAsync(animalFarmId, satireId);
await repo.AssignGenreToBookAsync(foundationId, sciFiId);


Console.WriteLine("--- Authors with books ---");
foreach (var a in await repo.GetAuthorsWithBooksAsync())
{
    Console.WriteLine($"{a.Name} ({a.Country}):");
    foreach (var b in a.Books)
        Console.WriteLine($"   • {b.Title} [{b.Year}]");
}

Console.WriteLine("\n--- Books after 1950 ---");
foreach (var b in await repo.GetBooksAfterYearAsync(1950))
    Console.WriteLine($"{b.Title} ({b.Year})");


Console.WriteLine("\n=== 1. Books with Genres (QueryMultiple) ===");
foreach (var b in await repo.GetBooksWithGenresAsync())
{
    var genreList = b.Genres.Count > 0
        ? string.Join(", ", b.Genres.Select(g => g.Name))
        : "No genres";
    Console.WriteLine($"• {b.Title} ({b.Year}) -> Genres: [{genreList}]");
}


Console.WriteLine("\n=== 2. SearchBooksAsync (ILIKE '%the%') ===");
foreach (var b in await repo.SearchBooksAsync("the"))
    Console.WriteLine($"• [{b.Id}] {b.Title} ({b.Year})");


Console.WriteLine("\n=== 3. Pagination: GetBooksPagedAsync (PageSize = 2) ===");
for (int page = 1; page <= 3; page++)
{
    var pagedBooks = (await repo.GetBooksPagedAsync(page, pageSize: 2)).ToList();
    Console.WriteLine($"Page {page}:");
    foreach (var b in pagedBooks)
        Console.WriteLine($"   • [{b.Id}] {b.Title} ({b.Year})");
}


Console.WriteLine("\n=== 4. Transaction: TransferBookAsync ===");
bool transferred = await repo.TransferBookAsync(foundationId, orwellId);
Console.WriteLine($"Transfer 'Foundation' to author #{orwellId}: {transferred}");

bool failedTransfer = await repo.TransferBookAsync(foundationId, newAuthorId: 999999);
Console.WriteLine($"Transfer 'Foundation' to non-existent author #999999: {failedTransfer} (rolled back safely)");

// Вернем книгу обратно Азимову
await repo.TransferBookAsync(foundationId, asimovId);


Console.WriteLine("\n=== 5. SQL Injection Demo ===");
string maliciousInput = "xyz%' OR 1=1 --";

Console.WriteLine($"Malicious payload: \"{maliciousInput}\"");

var leakedBooks = (await repo.SearchBooksVulnerableAsync(maliciousInput)).ToList();
Console.WriteLine($"[VULNERABLE Concatenation] Returned {leakedBooks.Count} books (ALL rows leaked!):");
foreach (var b in leakedBooks)
    Console.WriteLine($"   ! Leaked: {b.Title}");

var safeBooks = (await repo.SearchBooksAsync(maliciousInput)).ToList();
Console.WriteLine($"[SAFE Parameterized Dapper] Returned {safeBooks.Count} books (attack neutralized).");


Console.WriteLine("\n=== 6. PostgreSQL Stored Function (SELECT * FROM get_books_by_country(@country)) ===");
foreach (var row in await repo.GetBooksByCountryFuncAsync("UK"))
{
    Console.WriteLine($"• {row.BookTitle} ({row.BookYear}) — Author: {row.AuthorName}, Genres count: {row.GenreCount}");
}
