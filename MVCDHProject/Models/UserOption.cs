namespace MVCDHProject.Models
{
    public class UserOption
    {
        public string AllowedUserNameCharacters { get; set; } =
"abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
        public bool RequireUniqueEmail { get; set; }

    }
}
