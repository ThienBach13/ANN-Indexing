DROP TABLE IF EXISTS Movies;
DROP TABLE IF EXISTS MovieCentroids;

CREATE TABLE Movies (
    id INT PRIMARY KEY,
    title NVARCHAR(200),
    genre NVARCHAR(100),
    release_year INT,
    description NVARCHAR(MAX),
    embedding VECTOR(1536), -- Small model fits here
    cluster_id INT NULL
);

CREATE TABLE MovieCentroids (
    cluster_id INT PRIMARY KEY,
    centroid_vector VECTOR(1536)
);