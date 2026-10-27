-- ============================================================================
-- Схема базы данных "library" (PostgreSQL)
-- Включает таблицы authors, books, genres, book_genres и хранимую функцию
-- ============================================================================

DROP TABLE IF EXISTS book_genres CASCADE;
DROP TABLE IF EXISTS genres CASCADE;
DROP TABLE IF EXISTS books CASCADE;
DROP TABLE IF EXISTS authors CASCADE;

-- 1. Таблица авторов
CREATE TABLE authors (
    id      SERIAL PRIMARY KEY,
    name    VARCHAR(100) NOT NULL,
    country VARCHAR(50)
);

-- 2. Таблица книг
CREATE TABLE books (
    id        SERIAL PRIMARY KEY,
    title     VARCHAR(200) NOT NULL,
    year      INT,
    author_id INT NOT NULL REFERENCES authors(id) ON DELETE CASCADE
);

-- 3. Задание 1: Таблица жанров и связь многие-ко-многим (books <-> genres)
CREATE TABLE genres (
    id   SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE book_genres (
    book_id  INT NOT NULL REFERENCES books(id) ON DELETE CASCADE,
    genre_id INT NOT NULL REFERENCES genres(id) ON DELETE CASCADE,
    PRIMARY KEY (book_id, genre_id)
);

-- 4. Задание 6: Хранимая функция в PostgreSQL для вызова через Dapper
-- Возвращает книги выбранного автора (или всех авторов из указанной страны)
-- с именем автора и количеством жанров у каждой книги
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