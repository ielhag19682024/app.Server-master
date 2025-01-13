using Microsoft.EntityFrameworkCore;

namespace Generic.Server.Utilities
{
    public class DBContext : DbContext
    {
        
        public DBContext(DbContextOptions<DBContext> options) : base(options)
        {
        }

    }

}
