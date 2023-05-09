namespace applecat.Core
{
    public static class Slugcat
    {
        public static bool IsId(SlugcatStats.Name name, string id)
        {
            if (name == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            return string.Equals(name.value, id, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}