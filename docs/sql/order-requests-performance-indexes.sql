/*
   Order Requests external UPC database support indexes.

   This database is not owned by the application migration pipeline. Review
   the target database and existing indexes before applying. Run this script
   only against the approved UPC Testing database during validation; obtain
   separate approval before applying it to any Production database.

   The key order is intentional: the query keeps the newest header/invoice
   row per order number with ORDER BY Id DESC, while the INCLUDE columns cover
   the list projection and header-derived filters.
*/

IF OBJECT_ID(N'dbo.RequestOrderHeaders', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.RequestOrderHeaders')
         AND name = N'IX_RequestOrderHeaders_OrderNumber_Id'
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_RequestOrderHeaders_OrderNumber_Id
        ON dbo.RequestOrderHeaders (OrderNumber, Id DESC)
        INCLUDE (BranchCode, BranchName, OrderStatus, ParentOrderNumber, ConsumerMobile);
END;

IF OBJECT_ID(N'dbo.Invoices', N'U') IS NOT NULL
   AND NOT EXISTS (
       SELECT 1
       FROM sys.indexes
       WHERE object_id = OBJECT_ID(N'dbo.Invoices')
         AND name = N'IX_Invoices_OnlineOrderNumber_Id'
   )
BEGIN
    CREATE NONCLUSTERED INDEX IX_Invoices_OnlineOrderNumber_Id
        ON dbo.Invoices (OnlineOrderNumber, Id DESC)
        INCLUDE (Barcode, CloseDateLocalTime);
END;

/* Rollback (only after confirming the indexes are not needed elsewhere):
DROP INDEX IF EXISTS IX_RequestOrderHeaders_OrderNumber_Id ON dbo.RequestOrderHeaders;
DROP INDEX IF EXISTS IX_Invoices_OnlineOrderNumber_Id ON dbo.Invoices;
*/
