using Dapper;
using Npgsql;

public class LibraryRepository
{
    private readonly string _cs;
    public LibraryRepository(string connectionString) => _cs = connectionString;

    private NpgsqlConnection Conn() => new NpgsqlConnection(_cs);


    public async Task InitializeSchemaAsync()
    {
        using var c = Conn();
        const string sql = """
            DROP TABLE IF EXISTS book_genres CASCADE;
            DROP TABLE IF EXISTS genres CASCADE;
            DROP TABLE IF EXISTS books CASCADE;
            DROP TABLE IF EXISTS authors CASCADE;

            CREATE TABLE authors (
                id      SERIAL PRIMARY KEY,
                name    VARCHAR(100) NOT NULL,
                country VARCHAR(50)
            );

            CREATE TABLE books (
                id        SERIAL PRIMARY KEY,
                title     VARCHAR(200) NOT NULL,
                year      INT,
                author_id INT NOT NULL REFERENCES authors(id) ON DELETE CASCADE
            );

            CREATE TABLE genres (
                id   SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL UNIQUE
            );

            CREATE TABLE book_genres (
                book_id  INT NOT NULL REFERENCES books(id) ON DELETE CASCADE,
                genre_id INT NOT NULL REFERENCES genres(id) ON DELETE CASCADE,
                PRIMARY KEY (book_id, genre_id)
            );

            CREATE OR REPLACE FUNCTION get_books_by_country(p_country VARCHAR)
            RETURNS TABLE (
                book_id     INT,
                book_title  VARCHAR,
                book_year   INT,
                author_name VARCHAR,
                genre_count BIGINT
            )
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RETURN QUERY
                SELECT 
                    b.id AS book_id,
                    b.title AS book_title,
                    b.year AS book_year,
                    a.name AS author_name,
                    COUNT(bg.genre_id) AS genre_count
                FROM books b
                JOIN authors a ON a.id = b.author_id
                LEFT JOIN book_genres bg ON bg.book_id = b.id
                WHERE a.country ILIKE p_country
                GROUP BY b.id, b.title, b.year, a.name
                ORDER BY b.year, b.id;
            END;
            $$;
            """;
        await c.ExecuteAsync(sql);
    }


    public async Task<int> AddAuthorAsync(Author author)
    {
        using var c = Conn();
        const string sql = """
            INSERT INTO authors (name, country)
            VALUES (@Name, @Country)
            RETURNING id;
            """;
        return await c.ExecuteScalarAsync<int>(sql, author);
    }

    public async Task<int> AddBookAsync(Book book)
    {
        using var c = Conn();
        const string sql = """
            INSERT INTO books (title, year, author_id)
            VALUES (@Title, @Year, @AuthorId)
            RETURNING id;
            """;
        return await c.ExecuteScalarAsync<int>(sql, book);
    }


    public async Task<IEnumerable<Book>> GetBooksAsync()
    {
        using var c = Conn();
        return await c.QueryAsync<Book>("SELECT * FROM books ORDER BY id");
    }

    public async Task<IEnumerable<Book>> GetBooksAfterYearAsync(int year)
    {
        using var c = Conn();
        const string sql = "SELECT * FROM books WHERE year >= @year ORDER BY year";
        return await c.QueryAsync<Book>(sql, new { year });
    }

    // READ: маппинг один-ко-многим (автор + его книги)
    public async Task<IEnumerable<Author>> GetAuthorsWithBooksAsync()
    {
        using var c = Conn();
        const string sql = """
            SELECT a.id, a.name, a.country,
                   b.id, b.title, b.year, b.author_id
            FROM authors a
            LEFT JOIN books b ON b.author_id = a.id
            ORDER BY a.id;
            """;

        var authors = new Dictionary<int, Author>();
        await c.QueryAsync<Author, Book, Author>(
            sql,
            (author, book) =>
            {
                if (!authors.TryGetValue(author.Id, out var a))
                {
                    a = author;
                    authors[author.Id] = a;
                }
                if (book is not null) a.Books.Add(book);
                return a;
            },
            splitOn: "id"
        );
        return authors.Values;
    }

    // UPDATE
    public async Task<bool> UpdateBookYearAsync(int bookId, int newYear)
    {
        using var c = Conn();
        const string sql = "UPDATE books SET year = @newYear WHERE id = @bookId";
        var rows = await c.ExecuteAsync(sql, new { bookId, newYear });
        return rows > 0;
    }

    // DELETE
    public async Task<bool> DeleteAuthorAsync(int authorId)
    {
        using var c = Conn();
        var rows = await c.ExecuteAsync(
            "DELETE FROM authors WHERE id = @authorId",
            new { authorId });
        return rows > 0;
    }

    public async Task<int> AddGenreAsync(string name)
    {
        using var c = Conn();
        const string sql = """
            INSERT INTO genres (name)
            VALUES (@name)
            ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name
            RETURNING id;
            """;
        return await c.ExecuteScalarAsync<int>(sql, new { name });
    }

    public async Task AssignGenreToBookAsync(int bookId, int genreId)
    {
        using var c = Conn();
        const string sql = """
            INSERT INTO book_genres (book_id, genre_id)
            VALUES (@bookId, @genreId)
            ON CONFLICT DO NOTHING;
            """;
        await c.ExecuteAsync(sql, new { bookId, genreId });
    }

    public async Task<IEnumerable<Book>> GetBooksWithGenresAsync()
    {
        using var c = Conn();
        const string sql = """
            SELECT id, title, year, author_id
            FROM books
            ORDER BY id;

            SELECT bg.book_id, g.id, g.name
            FROM book_genres bg
            JOIN genres g ON g.id = bg.genre_id
            ORDER BY bg.book_id, g.id;
            """;

        using var multi = await c.QueryMultipleAsync(sql);
        var books = (await multi.ReadAsync<Book>()).ToDictionary(b => b.Id);
        var bookGenres = await multi.ReadAsync<BookGenreRow>();

        foreach (var bg in bookGenres)
        {
            if (books.TryGetValue(bg.BookId, out var book))
            {
                book.Genres.Add(new Genre { Id = bg.Id, Name = bg.Name });
            }
        }

        return books.Values;
    }

    public async Task<IEnumerable<Book>> SearchBooksAsync(string term)
    {
        using var c = Conn();
        const string sql = """
            SELECT id, title, year, author_id
            FROM books
            WHERE title ILIKE @pattern
            ORDER BY id;
            """;
        return await c.QueryAsync<Book>(sql, new { pattern = $"%{term}%" });
    }


    public async Task<IEnumerable<Book>> GetBooksPagedAsync(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        int offset = (page - 1) * pageSize;

        using var c = Conn();
        const string sql = """
            SELECT id, title, year, author_id
            FROM books
            ORDER BY id
            LIMIT @size OFFSET @offset;
            """;
        return await c.QueryAsync<Book>(sql, new { size = pageSize, offset });
    }

    public async Task<bool> TransferBookAsync(int bookId, int newAuthorId)
    {
        using var conn = Conn();
        await conn.OpenAsync();
        using var tx = await conn.BeginTransactionAsync();

        try
        {
            // Проверяем, существует ли целевой автор
            const string checkAuthorSql = "SELECT COUNT(1) FROM authors WHERE id = @newAuthorId;";
            var authorExists = await conn.ExecuteScalarAsync<int>(
                checkAuthorSql,
                new { newAuthorId },
                transaction: tx) > 0;

            if (!authorExists)
            {
                await tx.RollbackAsync();
                return false;
            }

            // Переводим книгу на нового автора
            const string updateBookSql = """
                UPDATE books
                SET author_id = @newAuthorId
                WHERE id = @bookId;
                """;
            var affected = await conn.ExecuteAsync(
                updateBookSql,
                new { bookId, newAuthorId },
                transaction: tx);

            if (affected == 0)
            {
                await tx.RollbackAsync();
                return false;
            }

            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<Book>> SearchBooksVulnerableAsync(string rawTerm)
    {
        using var c = Conn();
        // ОПАСНО: прямая подстановка пользовательского ввода в SQL-строку!
        // При вводе: xyz%' OR 1=1 --
        // Запрос превращается в:
        // SELECT id, title, year, author_id FROM books WHERE title ILIKE '%xyz%' OR 1=1 --%'
        string vulnerableSql = $"SELECT id, title, year, author_id FROM books WHERE title ILIKE '%{rawTerm}%'";
        return await c.QueryAsync<Book>(vulnerableSql);
    }
    public async Task<IEnumerable<CountryBookDto>> GetBooksByCountryFuncAsync(string country)
    {
        using var c = Conn();
        const string sql = "SELECT * FROM get_books_by_country(@country);";
        return await c.QueryAsync<CountryBookDto>(sql, new { country });
    }
}
