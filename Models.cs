public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Country { get; set; }
    public List<Book> Books { get; set; } = new();
}

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public int? Year { get; set; }
    public int AuthorId { get; set; }
    public List<Genre> Genres { get; set; } = new();
}

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}


public class BookGenreRow
{
    public int BookId { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = "";
}


public class CountryBookDto
{
    public int BookId { get; set; }
    public string BookTitle { get; set; } = "";
    public int? BookYear { get; set; }
    public string AuthorName { get; set; } = "";
    public long GenreCount { get; set; }
}
