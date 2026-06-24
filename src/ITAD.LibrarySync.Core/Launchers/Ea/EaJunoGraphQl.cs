namespace ITAD.LibrarySync.Core.Launchers.Ea;

internal static class EaJunoGraphQl
{
    internal const string IdentityQuery = "query{me{player{pd psd displayName}}}";

    internal const string OwnedGamesQuery =
        "query{me{ownedGameProducts(storefronts:[EA],locale:\"DEFAULT\",paging:{limit:9999,next:null}," +
        "productFound:true,orderBy:{field:NAME,direction:ASC}," +
        "ownershipMethod:[PURCHASE,REDEMPTION,ENTITLEMENT_GRANT]," +
        "type:[DIGITAL_FULL_GAME,PACKAGED_FULL_GAME],downloadableOnly:false,entitlementEnabled:true,platforms:[PC])" +
        "{items{id:originOfferId status product{id name downloadable gameSlug trialDetails{trialType} " +
        "baseItem(availabilities:[VISIBLE]){title id baseGameSlug gameType}}}}}}";
}
