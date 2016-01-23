# DiscogsWatcher
A program for checking one's wishlist on Discogs, going through new listings for each release and sending an email to the user if they meet prespecified price requirements.

## Database setup

    CREATE TABLE [dbo].[Listings] (
        [ListingId] INT           NOT NULL,
        [ReleaseId] INT           NOT NULL,
        [Price]     DECIMAL (18)  NOT NULL,
        [Currency]  NVARCHAR (3)  NOT NULL,
        [ShipsFrom] NVARCHAR (30) NOT NULL
    );
    
    
## Discogs setup
Add desired releases to wantlist and use the Notes field to set the price limit. New listings that are more expensive and listings for releases without the price limit set will not trigger an email notification. The price limit and the listing price are compared as simple integeres, regardless of the listed currency (EUR in most cases in my experience),
