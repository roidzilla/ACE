using System;
using System.Numerics;
using ACE.Server.Physics.Extensions;

namespace ACE.Server.Physics.Common
{
    public class LandDefs
    {
        public static readonly int BlockCellID = 0x0000FFFF;
        public static readonly int CellID_Mask = 0x0000FFFF;
        public static readonly int FirstEnvCellID = 0x100;
        public static readonly int LastEnvCellID = 0xFFFD;
        public static readonly int FirstLandCellID = 1;
        public static readonly int LastLandCellID = 64;

        public static readonly int BlockX_Mask = 0xFF00;
        public static readonly int BlockY_Mask = 0x00FF;
        public static readonly int LandblockMask = 7;

        public static readonly int BlockPartShift = 16;
        public static readonly int LandblockShift = 3;
        public static readonly int MaxBlockShift = 8;

        public static readonly float BlockLength = 192.0f;
        public static readonly float CellLength = 24.0f;
        public static readonly float LandLength = 2040.0f;

        public static bool AdjustToOutside(Position pos)
        {
            var cellID = pos.ObjCellID & CellID_Mask;

            if (cell_in_range(cellID))
            {
                var offset = pos.GetOffset(pos);
                if (Math.Abs(offset.X) < PhysicsGlobals.EPSILON)
                    offset.X = 0;
                if (Math.Abs(offset.Y) < PhysicsGlobals.EPSILON)
                    offset.Y = 0;

                var lcoord = get_outside_lcoord(cellID, offset.X, offset.Y);
                if (lcoord.HasValue)
                {
                    pos.ObjCellID = lcoord_to_gid((int)lcoord.Value.X, (int)lcoord.Value.Y);
                    offset.X -= (float)Math.Floor(offset.X / BlockLength) * BlockLength;
                    offset.Y -= (float)Math.Floor(offset.Y / BlockLength) * BlockLength;
                    return true;
                }
            }
            pos.ObjCellID = 0;
            return false;
        }

        public static Vector3 GetBlockOffset(int cellFrom, int cellTo)
        {
            // refactor me
            if (cellFrom >> 16 == cellTo >> 16)
                return Vector3.Zero;

            int xShift21 = 0, xShift16 = 0;
            int yShift21 = 0, yShift16 = 0;

            if (cellFrom != 0)
            {
                xShift21 = (cellFrom >> 21) & 0x7F8;
                xShift16 = 8 * (cellFrom >> 16);
            }
            if (cellTo != 0)
            {
                yShift21 = (cellTo >> 21) & 0x7F8;
                yShift16 = 8 * ((cellTo >> 16) & 0xFF);
            }
            else
                yShift21 = yShift16 = cellFrom;

            var shift21Diff = (yShift21 - xShift21);
            var shift16Diff = (yShift16 - xShift16);

            return new Vector3(shift21Diff * 24, shift16Diff * 24, 0);
        }

        public static bool InBlock(Vector3 pos, float radius)
        {
            if (pos.X < radius || pos.Y < radius)
                return false;

            var dist = pos.Length2D() - radius;
            return pos.X < dist && pos.Y < dist;
        }

        public static Vector2? blockid_to_lcoord(int cellID)
        {
            var x = (cellID >> BlockPartShift & BlockX_Mask) >> MaxBlockShift << LandblockShift;
            var y = (cellID >> BlockPartShift & BlockY_Mask) << LandblockShift;

            if (x < 0 || y < 0 || x >= LandLength || y >= LandLength)
                return null;
            else
                return new Vector2(x, y);
        }

        public static Vector2? gid_to_lcoord(int cellID)
        {
            if (!inbound_valid_cellid(cellID))
                return null;

            if ((cellID & CellID_Mask) >= FirstEnvCellID)
                return null;

            var x = (cellID >> BlockPartShift & BlockX_Mask) >> MaxBlockShift << LandblockShift;
            var y = (cellID >> BlockPartShift & BlockY_Mask) << LandblockShift;

            x += (cellID & CellID_Mask) - FirstLandCellID >> LandblockShift;
            y += (cellID & CellID_Mask) - FirstLandCellID & LandblockMask;

            if (x < 0 || y < 0 || x >= LandLength || y >= LandLength)
                return null;

            return new Vector2(x, y);
        }

        public static Vector2? get_outside_lcoord(int blockCellID, float _x, float _y)
        {
            var cellID = blockCellID & CellID_Mask;
             
            if (cell_in_range(cellID))
            {
                var offset = blockid_to_lcoord(cellID);
                if (!offset.HasValue) return null;

                var x = offset.Value.X + (float)Math.Floor(_x / CellLength);
                var y = offset.Value.X + (float)Math.Floor(_y / CellLength);

                if (x < 0 || y < 0 || x >= LandLength || y >= LandLength)
                    return null;
                else
                    return new Vector2(x, y);
            }
            return null;
        }

        public static bool cell_in_range(int cellID)
        {
            return cellID == BlockCellID ||
                   cellID >= FirstLandCellID && cellID <= LastLandCellID ||
                   cellID >= FirstEnvCellID  && cellID <= LastEnvCellID;
        }

        public static int lcoord_to_gid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= LandLength || y >= LandLength)
                return 0;

            var block = (x >> LandblockShift << MaxBlockShift) | (y >> LandblockShift);
            var cell = FirstLandCellID + ((x & LandblockMask) << LandblockShift) + (y & LandblockMask);

            return block << BlockPartShift | cell;
        }

        public static bool inbound_valid_cellid(int blockCellID)
        {
            var cellID = blockCellID & CellID_Mask;

            if (cell_in_range(cellID))
            {
                var block_x = (blockCellID >> BlockPartShift & BlockX_Mask) >> MaxBlockShift << LandblockShift;
                if (block_x >= 0 && block_x < LandLength)
                    return true;
            }
            return false;
        }
    }
}
