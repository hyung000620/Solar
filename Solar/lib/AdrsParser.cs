using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Solar
{
    public class AdrsParser
    {
        public string Adrs { get; set; } = "";
        public string AdrsType { get; set; } = "none";
        public string Sido { get; set; } = "";
        public string SidoOrg { get; set; } = "";
        public string Gugun { get; set; } = "";
        public string Dong { get; set; } = "";
        public string Ri { get; set; } = "";
        public string Hanja { get; set; } = "";
        public string Mt { get; set; } = "";
        public string JibunM { get; set; } = "";
        public string JibunS { get; set; } = "";
        public string RoadNm { get; set; } = "";
        public string BldgM { get; set; } = "";
        public string BldgS { get; set; } = "";
        public string BldgNm { get; set; } = "";
        public string Ho { get; set; } = "";
        public string AdrsM { get; set; } = "";
        public string AdrsS { get; set; } = "";

        public string patnJibun = @"^(\w+[시도]|서울|대전|대구|부산|광주|울산|인천|제주|세종|경기|강원|경북|경남|전북|전남|충북|충남)[ ]*(\w+시[ ]*\w+구\b|\w+[시구군]\b)*[ ]*(\w+[읍면동가]\b)*[ ]*(\w+리\b)*[ ]*(\([一-龥]*\))*[ ]*(산)*(\d+)*[-]*(\d+)*(.*)";
        public string patnRoad = @"^(\w+[시도]|서울|대전|대구|부산|광주|울산|인천|제주|세종|경기|강원|경북|경남|전북|전남|충북|충남)[ ]*(\w+시[ ]*\w+구\b|\w+[시구군]\b)*[ ]*(\w+[읍면]\b)*[ ]*([\w\.\d]*(로|길|거리|고개|국도))*[ ]*(\d+)*[-]*(\d+)*([, \w\.\-\~\:\'\`㎡]*)[ ]*(\(.*\))*";

        public AdrsParser()
        {
            //
        }

        public AdrsParser(string adrs)
        {
            if (adrs.Contains("세종특별자치시")) adrs = adrs.Replace("세종특별자치시", "세종특별자치시 세종시");
            this.Adrs = adrs;

            Parsing();
        }

        private void Parsing()
        {
            Match mr = Regex.Match(Adrs, patnRoad, RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Match mj = Regex.Match(Adrs, patnJibun, RegexOptions.Multiline | RegexOptions.IgnoreCase);

            if (mr.Success == false) return;

            //공통 항목
            SidoOrg = mr.Groups[1].Value.Trim();
            Sido = Regex.Replace(mr.Groups[1].Value.Trim(), @"광역시|[특별시자치도상라청]", string.Empty, RegexOptions.Multiline);
            Gugun = mr.Groups[2].Value.Trim();

            if (Regex.Match(mj.Groups[3].Value, @"\w+[읍면동가]\b").Value != string.Empty && mr.Groups[5].Value.Trim() == string.Empty)
            {
                AdrsType = "jibunType";
                Dong = mj.Groups[3].Value.Trim();
                Ri = mj.Groups[4].Value.Trim();
                //Hanja = mj.Groups[5].Value;
                Mt = (mj.Groups[6].Value == "산") ? "2" : "1";
                JibunM = mj.Groups[7].Value.Trim();
                JibunS = mj.Groups[8].Value.Trim();
                AdrsS = mj.Groups[9].Value.Trim();
                AdrsM = Sido + " " + Gugun + " " + Dong + " " + Ri + " " + mj.Groups[6].Value + " " + JibunM;
                if (JibunS != string.Empty) AdrsM = AdrsM + "-" + JibunS;
                AdrsM = Regex.Replace(AdrsM, @"\s+", " ");
                if (mj.Groups[5].Value.Trim() != string.Empty) Hanja = Regex.Replace(mj.Groups[5].Value.Trim(), @"[\(\)]", string.Empty);
            }
            else
            {
                AdrsType = "roadType";
                Dong = mr.Groups[3].Value;
                RoadNm = mr.Groups[4].Value;
                BldgM = mr.Groups[6].Value;
                BldgS = mr.Groups[7].Value;
                Ho = Regex.Replace(mr.Groups[8].Value.Trim(), @"^[,]", string.Empty).Trim();
                AdrsS = mr.Groups[9].Value.Trim();
                AdrsM = Sido + " " + Gugun + " " + Dong + " " + RoadNm + " " + BldgM;
                if (BldgS != string.Empty) AdrsM = AdrsM + "-" + BldgS;
                AdrsM = Regex.Replace(AdrsM, @"\s+", " ");

                Match m = Regex.Match(AdrsS, @"\((\w+([동가리]))*[, ]*(\w+)*\)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (m.Success == true)
                {
                    if (Dong == string.Empty)
                    {
                        if (m.Groups[2].Value == "동" || m.Groups[2].Value == "가") Dong = m.Groups[1].Value;
                    }
                    if (m.Groups[2].Value == "리") Ri = m.Groups[1].Value;
                    BldgNm = m.Groups[3].Value.Trim();
                }
            }
        }
    }
}
