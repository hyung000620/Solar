using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Solar
{
    public class CoordCal
    {
        public double calDistance(double P1_latitude/*C10*/, double P1_longitude/*C11*/, double P2_latitude/*C12*/, double P2_longitude/*C13*/)
        {
            if ((P1_latitude == P2_latitude) && (P1_longitude == P2_longitude))
            {
                return 0;
            }

            double e10 = P1_latitude * Math.PI / 180;
            double e11 = P1_longitude * Math.PI / 180;
            double e12 = P2_latitude * Math.PI / 180;
            double e13 = P2_longitude * Math.PI / 180;

            /* 타원체 GRS80 */
            double c16 = 6356752.314140910;
            double c15 = 6378137.000000000;
            double c17 = 0.0033528107;


            double f15 = c17 + c17 * c17;
            double f16 = f15 / 2;
            double f17 = c17 * c17 / 2;
            double f18 = c17 * c17 / 8;
            double f19 = c17 * c17 / 16;

            double c18 = e13 - e11;
            double c20 = (1 - c17) * Math.Tan(e10);
            double c21 = Math.Atan(c20);
            double c22 = Math.Sin(c21);
            double c23 = Math.Cos(c21);
            double c24 = (1 - c17) * Math.Tan(e12);
            double c25 = Math.Atan(c24);
            double c26 = Math.Sin(c25);
            double c27 = Math.Cos(c25);

            double c29 = c18;
            double c31 = (c27 * Math.Sin(c29) * c27 * Math.Sin(c29)) + (c23 * c26 - c22 * c27 * Math.Cos(c29)) * (c23 * c26 - c22 * c27 * Math.Cos(c29));
            double c33 = (c22 * c26) + (c23 * c27 * Math.Cos(c29));
            double c35 = Math.Sqrt(c31) / c33;
            double c36 = Math.Atan(c35);
            double c38 = 0;
            if (c31 == 0)
            {
                c38 = 0;
            }
            else
            {
                c38 = c23 * c27 * Math.Sin(c29) / Math.Sqrt(c31);
            }

            double c40 = 0;
            if ((Math.Cos(Math.Asin(c38)) * Math.Cos(Math.Asin(c38))) == 0)
            {
                c40 = 0;
            }
            else
            {
                c40 = c33 - 2 * c22 * c26 / (Math.Cos(Math.Asin(c38)) * Math.Cos(Math.Asin(c38)));
            }

            double c41 = Math.Cos(Math.Asin(c38)) * Math.Cos(Math.Asin(c38)) * (c15 * c15 - c16 * c16) / (c16 * c16);
            double c43 = 1 + c41 / 16384 * (4096 + c41 * (-768 + c41 * (320 - 175 * c41)));
            double c45 = c41 / 1024 * (256 + c41 * (-128 + c41 * (74 - 47 * c41)));
            double c47 = c45 * Math.Sqrt(c31) * (c40 + c45 / 4 * (c33 * (-1 + 2 * c40 * c40) - c45 / 6 * c40 * (-3 + 4 * c31) * (-3 + 4 * c40 * c40)));
            double c50 = c17 / 16 * Math.Cos(Math.Asin(c38)) * Math.Cos(Math.Asin(c38)) * (4 + c17 * (4 - 3 * Math.Cos(Math.Asin(c38)) * Math.Cos(Math.Asin(c38))));
            double c52 = c18 + (1 - c50) * c17 * c38 * (Math.Acos(c33) + c50 * Math.Sin(Math.Acos(c33)) * (c40 + c50 * c33 * (-1 + 2 * c40 * c40)));

            double c54 = c16 * c43 * (Math.Atan(c35) - c47);

            // return distance in meter
            return c54;
        }

        public short bearingP1toP2(double P1_latitude, double P1_longitude, double P2_latitude, double P2_longitude)
        {
            // 현재 위치 : 위도나 경도는 지구 중심을 기반으로 하는 각도이기 때문에 라디안 각도로 변환한다.
            double Cur_Lat_radian = P1_latitude * (3.141592 / 180);
            double Cur_Lon_radian = P1_longitude * (3.141592 / 180);


            // 목표 위치 : 위도나 경도는 지구 중심을 기반으로 하는 각도이기 때문에 라디안 각도로 변환한다.
            double Dest_Lat_radian = P2_latitude * (3.141592 / 180);
            double Dest_Lon_radian = P2_longitude * (3.141592 / 180);

            // radian distance
            double radian_distance = 0;
            radian_distance = Math.Acos(Math.Sin(Cur_Lat_radian) * Math.Sin(Dest_Lat_radian) + Math.Cos(Cur_Lat_radian) * Math.Cos(Dest_Lat_radian) * Math.Cos(Cur_Lon_radian - Dest_Lon_radian));

            // 목적지 이동 방향을 구한다.(현재 좌표에서 다음 좌표로 이동하기 위해서는 방향을 설정해야 한다. 라디안값이다.
            double radian_bearing = Math.Acos((Math.Sin(Dest_Lat_radian) - Math.Sin(Cur_Lat_radian) * Math.Cos(radian_distance)) / (Math.Cos(Cur_Lat_radian) * Math.Sin(radian_distance)));		// acos의 인수로 주어지는 x는 360분법의 각도가 아닌 radian(호도)값이다.		

            double true_bearing = 0;
            if (Math.Sin(Dest_Lon_radian - Cur_Lon_radian) < 0)
            {
                true_bearing = radian_bearing * (180 / 3.141592);
                true_bearing = 360 - true_bearing;
            }
            else
            {
                true_bearing = radian_bearing * (180 / 3.141592);
            }

            return (short)true_bearing;
        }
    }
}
