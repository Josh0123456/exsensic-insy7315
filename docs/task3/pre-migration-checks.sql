-- Read-only checks to run against the target database (staging, then production) before the
-- migration AddBookingRelatedConstraintsAndIndexes is deployed to it.
-- It only reads dbo.BookingRequirements. Every row must say PASS; otherwise stop and fix the data first.
--
-- Run in the Azure portal: SQL database -> Query editor, or:
--   sqlcmd -S <server>.database.windows.net -d <database> -G -i docs/task3/pre-migration-checks.sql

SET NOCOUNT ON;

-- 1. The new unique index on (BookingId, FieldKey) can't be created while duplicates exist.
--    GROUP BY uses the column's collation, so it finds the same duplicates the index would.
SELECT
    'Duplicate (BookingId, FieldKey) in BookingRequirements' AS [Check],
    CASE WHEN COUNT(*) = 0 THEN 'PASS'
         ELSE CAST(COUNT(*) AS varchar(20)) + ' duplicate group(s)' END AS Result
FROM (
    SELECT BookingId, FieldKey
    FROM dbo.BookingRequirements
    GROUP BY BookingId, FieldKey
    HAVING COUNT(*) > 1
) AS duplicates

UNION ALL

-- 2. FieldKey narrows from nvarchar(max) to nvarchar(50). DATALENGTH counts trailing spaces, which LEN ignores.
SELECT
    'FieldKey longer than 50 characters',
    CASE WHEN COUNT(*) = 0 THEN 'PASS'
         ELSE CAST(COUNT(*) AS varchar(20)) + ' row(s)' END
FROM dbo.BookingRequirements
WHERE DATALENGTH(FieldKey) / 2 > 50;
