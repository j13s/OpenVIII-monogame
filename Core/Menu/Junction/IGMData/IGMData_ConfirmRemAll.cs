﻿using Microsoft.Xna.Framework;

namespace OpenVIII
{
    public partial class Junction
    {
        #region Classes

        private sealed class IGMData_ConfirmRemAll : IGMData.Dialog.Confirm
        {
            #region Methods

            public static IGMData_ConfirmRemAll Create(FF8String data, Icons.ID title, FF8String opt1, FF8String opt2, Rectangle pos) =>
                Create<IGMData_ConfirmRemAll>(data, title, opt1, opt2, pos, 1);

            public override bool Inputs_CANCEL()
            {
                base.Inputs_CANCEL();
                Junction.Data[SectionName.RemAll].Hide();
                Junction.SetMode(Mode.TopMenu_Off);
                return true;
            }

            public override bool Inputs_OKAY()
            {
                switch (CURSOR_SELECT)
                {
                    case 0:
                        skipsnd = true;
                        AV.Sound.Play(31);
                        base.Inputs_OKAY();
                        if (Damageable.GetCharacterData(out var c))
                            c.RemoveAll();
                        Junction.Data[SectionName.RemAll].Hide();
                        Junction.Data[SectionName.TopMenu_Off].Hide();
                        Junction.SetMode(Mode.TopMenu);
                        ((IGMData.Base)Junction.Data[SectionName.TopMenu]).CURSOR_SELECT = 0;
                        Junction.Refresh();
                        break;

                    case 1:
                        Inputs_CANCEL();
                        break;

                    default: return false;
                }
                return true;
            }

            #endregion Methods
        }

        #endregion Classes
    }
}