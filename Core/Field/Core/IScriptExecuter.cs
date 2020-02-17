﻿using OpenVIII.Fields.Scripts.Instructions;
using System.Collections.Generic;

namespace OpenVIII.Fields
{
    public interface IScriptExecuter
    {
        IEnumerable<IAwaitable> Execute(IServices services);
    }
}