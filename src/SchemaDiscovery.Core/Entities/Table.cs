using System;
using System.Collections.Generic;
using System.Text;

namespace SchemaDiscovery.Entities
{
    public class Table : DatabaseObject, IEquatable<Table>
    {
        public bool Equals(Table? other)
        {
            throw new NotImplementedException();
        }
    }
}
