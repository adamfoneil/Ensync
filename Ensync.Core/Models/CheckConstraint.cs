﻿using Ensync.Core.Abstract;

namespace Ensync.Core.Models;

public class CheckConstraint : DbObject
{
    public override DbObjectType Type => DbObjectType.CheckConstraint;
}
