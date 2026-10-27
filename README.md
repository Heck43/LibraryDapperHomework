# Домашнее задание: Работа с PostgreSQL через Dapper (C# / .NET)

Проект содержит выполнение всех 6 заданий самостоятельной работы по теме **Dapper + Npgsql + PostgreSQL**.

## Структура проекта

* `schema.sql` — SQL-скрипт создания таблиц (`authors`, `books`, `genres`, `book_genres`) и хранимой функции `get_books_by_country(p_country VARCHAR)`.
* `Program.cs` — запуск приложения и демонстрация работы всех методов.
* `Models.cs` — модели данных и DTO.
* `LibraryRepository.cs` — работа с PostgreSQL через Dapper.

## Реализованные задания

| № | Задание | Метод в `LibraryRepository` | Особенности реализации |
| :--- | :--- | :--- | :--- |
| 1 | **Жанры (`books ↔ genres`)** | `GetBooksWithGenresAsync()`, `AddGenreAsync()`, `AssignGenreToBookAsync()` | Таблицы `genres` и `book_genres` (M:N). Загрузка книг и их жанров одним обращением к БД через `QueryMultipleAsync` (два `SELECT` в одном пакете). |
| 2 | **Поиск по названию** | `SearchBooksAsync(string term)` | Регистронезависимый поиск через `WHERE title ILIKE @pattern` с параметром `$"%{term}%"`. |
| 3 | **Пагинация** | `GetBooksPagedAsync(int page, int pageSize)` | Вычисление смещения `offset = (page - 1) * pageSize` и запрос `LIMIT @size OFFSET @offset`. |
| 4 | **Транзакция** | `TransferBookAsync(int bookId, int newAuthorId)` | Открытие соединения `conn.OpenAsync()`, старт транзакции `conn.BeginTransactionAsync()`, проверка существования автора, обновление `author_id` и `CommitAsync()` / `RollbackAsync()`. |
| 5 | **SQL-инъекция** | `SearchBooksVulnerableAsync(string rawTerm)` vs `SearchBooksAsync(string term)` | Наглядное сравнение: строка `"xyz%' OR 1=1 --"` при конкатенации возвращает всю таблицу книг, а при передаче через параметры Dapper (`@pattern`) ищется буквально как текст и не ломает SQL-запрос. |
| 6 | **Хранимая функция PostgreSQL** | `GetBooksByCountryFuncAsync(string country)` | PL/pgSQL функция `get_books_by_country(p_country VARCHAR)`, вызываемая через Dapper запросом `SELECT * FROM get_books_by_country(@country);`. |

## Как запустить локально

1. Поднимите PostgreSQL (например, через Docker):
   ```bash
   docker run --name pg-library -e POSTGRES_USER=lab -e POSTGRES_PASSWORD=lab123 -e POSTGRES_DB=library -p 5432:5432 -d postgres:16
   ```

2. В этом проекте зависимости `Dapper` и `Npgsql` уже указаны. При создании проекта с нуля установите их:
   ```bash
   dotnet new console -n LibraryDapperApp
   cd LibraryDapperApp
   dotnet add package Dapper
   dotnet add package Npgsql
   ```

3. В корне проекта запустите приложение:
   ```bash
   dotnet run
   ```
   *(Метод `InitializeSchemaAsync()` автоматически создаст таблицы и хранимую функцию при старте, либо вы можете выполнить `schema.sql` вручную через `psql` / pgAdmin).*

## Как сдать ДЗ в GitHub

```bash
git init
git add Program.cs Models.cs LibraryRepository.cs schema.sql README.md
git commit -m "feat: complete Dapper PostgreSQL homework tasks"
git branch -M main
git remote add origin https://github.com/<ваш-логин>/<ваш-репозиторий>.git
git push -u origin main
```
