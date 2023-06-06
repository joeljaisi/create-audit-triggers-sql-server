# create-audit-triggers-sql-server
The application written in C# using .net 6, allows a user to connect to an MS SQL Server database and be able to generate triggers and audit tables. It allows the user to select the table and columns to be audited, and which triggers to be created (Update and Delete created by default and Insert trigger is optional).

The app creates the audit tables named after the base table being audited, appending a suffix. For example if you are auditing a table named product, the audit table will be named product_audit. The triggers t-sql statements are all in one file named after the base table, with a suffix, e.g. product_triggers.

Contributors welcome.
