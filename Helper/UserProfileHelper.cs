using InventarioAPI.Models;

namespace InventarioAPI.Helpers
{
    public static class UserProfileHelper
    {
        public static UserProfile ParseFromDatabase(string dbValue)
        {
            return dbValue?.ToUpper() switch
            {
                "ADMINISTRADOR" => UserProfile.ADMINISTRADOR,
                "GERENTE_TIENDA" => UserProfile.GERENTE_TIENDA,
                "LIDER" => UserProfile.LIDER,
                "INVENTARIO" => UserProfile.INVENTARIO,
                _ => UserProfile.INVENTARIO // Default
            };
        }

        public static string ToDatabase(UserProfile profile)
        {
            return profile switch
            {
                UserProfile.ADMINISTRADOR => "ADMINISTRADOR",
                UserProfile.GERENTE_TIENDA => "GERENTE_TIENDA",
                UserProfile.LIDER => "LIDER",
                UserProfile.INVENTARIO => "INVENTARIO",
                _ => "INVENTARIO"
            };
        }

        public static bool IsAdmin(UserProfile profile)
        {
            return profile ==  UserProfile.ADMINISTRADOR;
        }

        public static bool IsManager(UserProfile profile)
        {
            return IsAdmin(profile) || profile == UserProfile.GERENTE_TIENDA;
        }

        // Métodos adicionales para trabajar con strings (para JWT claims)
        public static bool IsAdmin(string? profileString)
        {
            return profileString == "ADMIN" || profileString == "ADMINISTRADOR";
        }

        public static bool IsManager(string? profileString)
        {
            return IsAdmin(profileString) || profileString == "GERENTE_TIENDA";
        }
    }
}