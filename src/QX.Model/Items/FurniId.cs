namespace Qx.Model;

public static class FurniId
{
    const long builders_club_min = 2147418112;
    const long temporary_min = 2147401728;

    public static bool IsFromBuildersClub(Id id) =>
        (long)id is >= builders_club_min and <= int.MaxValue;

    public static bool IsTemporary(Id id) =>
        (long)id is >= temporary_min and < builders_club_min;

    public static bool IsNormal(Id id) =>
        (long)id is > 0 and < temporary_min;
}
