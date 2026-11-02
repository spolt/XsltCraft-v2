<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0"
                xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                xmlns:ccts="urn:un:unece:uncefact:documentation:2"
                xmlns:clm54217="urn:un:unece:uncefact:codelist:specification:54217:2001"
                xmlns:clm5639="urn:un:unece:uncefact:codelist:specification:5639:1988"
                xmlns:clm66411="urn:un:unece:uncefact:codelist:specification:66411:2001"
                xmlns:clmIANAMIMEMediaType="urn:un:unece:uncefact:codelist:specification:IANAMIMEMediaType:2003"
                xmlns:fn="http://www.w3.org/2005/xpath-functions"
                xmlns:link="http://www.xbrl.org/2003/linkbase"
                xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:DespatchAdvice-2"
                xmlns:qdt="urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2"
                xmlns:udt="urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2"
                xmlns:xbrldi="http://xbrl.org/2006/xbrldi"
                xmlns:xbrli="http://www.xbrl.org/2003/instance"
                xmlns:xdt="http://www.w3.org/2005/xpath-datatypes"
                xmlns:xlink="http://www.w3.org/1999/xlink"
                xmlns:xs="http://www.w3.org/2001/XMLSchema"
                xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                exclude-result-prefixes="cac cbc ccts clm54217 clm5639 clm66411 clmIANAMIMEMediaType fn link n1 qdt udt xbrldi xbrli xdt xlink xs xsd xsi">
  <xsl:character-map name="a">
    <xsl:output-character character="&#128;" string=""/>
    <xsl:output-character character="&#129;" string=""/>
    <xsl:output-character character="&#130;" string=""/>
    <xsl:output-character character="&#131;" string=""/>
    <xsl:output-character character="&#132;" string=""/>
    <xsl:output-character character="&#133;" string=""/>
    <xsl:output-character character="&#134;" string=""/>
    <xsl:output-character character="&#135;" string=""/>
    <xsl:output-character character="&#136;" string=""/>
    <xsl:output-character character="&#137;" string=""/>
    <xsl:output-character character="&#138;" string=""/>
    <xsl:output-character character="&#139;" string=""/>
    <xsl:output-character character="&#140;" string=""/>
    <xsl:output-character character="&#141;" string=""/>
    <xsl:output-character character="&#142;" string=""/>
    <xsl:output-character character="&#143;" string=""/>
    <xsl:output-character character="&#144;" string=""/>
    <xsl:output-character character="&#145;" string=""/>
    <xsl:output-character character="&#146;" string=""/>
    <xsl:output-character character="&#147;" string=""/>
    <xsl:output-character character="&#148;" string=""/>
    <xsl:output-character character="&#149;" string=""/>
    <xsl:output-character character="&#150;" string=""/>
    <xsl:output-character character="&#151;" string=""/>
    <xsl:output-character character="&#152;" string=""/>
    <xsl:output-character character="&#153;" string=""/>
    <xsl:output-character character="&#154;" string=""/>
    <xsl:output-character character="&#155;" string=""/>
    <xsl:output-character character="&#156;" string=""/>
    <xsl:output-character character="&#157;" string=""/>
    <xsl:output-character character="&#158;" string=""/>
    <xsl:output-character character="&#159;" string=""/>
  </xsl:character-map>
  <xsl:decimal-format name="european" decimal-separator="," grouping-separator="." NaN=""/>
  <xsl:output version="4.0" method="html" indent="no" encoding="UTF-8"
              doctype-public="-//W3C//DTD HTML 4.01 Transitional//EN"
              doctype-system="http://www.w3.org/TR/html4/loose.dtd" use-character-maps="a"/>
  <xsl:param name="SV_OutputFormat" select="'HTML'"/>
  <xsl:variable name="XML" select="/"/>
  <xsl:variable name="varStockCode" select="count(//cac:SellersItemIdentification[cbc:ID !=''])"/>
  <xsl:variable name="varInnerItemCount" select="count(//cac:GoodsItem[cbc:Description !=''])"/>
  <xsl:variable name="varManufacturerItemId" select="count(//cac:ManufacturersItemIdentification[cbc:ID !=''])"/>
  <xsl:variable name="varBuyersItemId" select="count(//cac:BuyersItemIdentification[cbc:ID !=''])"/>
  <xsl:variable name="varLineDescription" select="count(//cac:DespatchLine[cbc:Note !=''])"/>
  <xsl:variable name="varKunyeNo" select="//n1:DespatchAdvice/cbc:ProfileID='HALTIPI' and count(//cbc:ID[@schemeID='KUNYENO'])>0"/>
  <!--  <xsl:variable name="varOrderLineRef" select="count(//cac:OrderLineReference[cbc:LineID !='0'])"/>-->

  <xsl:template match="/">
    <html>
      <head>
        <style type="text/css">
          body {
          background-color: #FFFFFF;
          font-family: 'Tahoma', "Times New Roman", Times, serif;
          font-size: 11px;
          color: #000000;
          }
          h1, h2 {
          padding-bottom: 3px;
          padding-top: 3px;
          margin-bottom: 5px;
          text-transform: uppercase;
          font-family: Arial, Helvetica, sans-serif;
          }
          h1 {
          font-size: 1.4em;
          text-transform:none;
          }
          h2 {
          font-size: 1em;
          color: brown;
          }
          h3 {
          font-size: 1em;
          color: #000000;
          text-align: justify;
          margin: 0;
          padding: 0;
          }
          h4 {
          font-size: 1.1em;
          font-style: bold;
          font-family: Arial, Helvetica, sans-serif;
          color: #000000;
          margin: 0;
          padding: 0;
          }
          hr {
          height:2px;
          color: #000000;
          background-color: #000000;
          border-bottom: 1px solid #000000;
          }
          p, ul, ol {
          margin-top: 1.5em;
          }
          ul, ol {
          margin-left: 3em;
          }
          blockquote {
          margin-left: 3em;
          margin-right: 3em;
          font-style: italic;
          }
          a {
          text-decoration: none;
          color: #70A300;
          }
          a:hover {
          border: none;
          color: #70A300;
          }
          #despatchTable {
          border-collapse:collapse;
          font-size:11px;
          float:right;
          border-color:gray;
          }
          #ettnTable {
          border-collapse:collapse;
          font-size:11px;
          border-color:gray;
          }
          #customerPartyTable {
          border-width: 0px;
          border-spacing:;
          border-style: inset;
          border-color: gray;
          border-collapse: collapse;
          background-color:
          }
          #customerIDTable {
          border-width: 2px;
          border-spacing:;
          border-style: inset;
          border-color: gray;
          border-collapse: collapse;
          background-color:
          }
          #customerIDTableTd {
          border-width: 2px;
          border-spacing:;
          border-style: inset;
          border-color: gray;
          border-collapse: collapse;
          background-color:
          }
          #lineTable {
          border-width:2px;
          border-spacing:;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color:;
          }
          td.lineTableTd {
          border-width: 1px;
          padding: 1px;
          border-style: inset;
          border-color: black;
          background-color: white;
          }
          tr.lineTableTr {
          border-width: 1px;
          padding: 0px;
          border-style: inset;
          border-color: black;
          background-color: white;
          -moz-border-radius:;
          }
          #lineTableDummyTd {
          border-width: 1px;
          border-color:white;
          padding: 1px;
          border-style: inset;
          border-color: black;
          background-color: white;
          }
          td.lineTableBudgetTd {
          border-width: 2px;
          border-spacing:0px;
          padding: 1px;
          border-style: inset;
          border-color: black;
          background-color: white;
          -moz-border-radius:;
          }
          #notesTable {
          border-width: 2px;
          border-spacing:;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color:
          }
          #notesTableTd {
          border-width: 0px;
          border-spacing:;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color: ;
          vertical-align: top;
          }
          table {
          border-spacing:0px;
          }
          #budgetContainerTable {
          border-width: 0px;
          border-spacing: 0px;
          border-style: inset;
          border-color: black;
          border-collapse: collapse;
          background-color:;
          }
          td {
          border-color:gray;
          }

          #invoice-info-td {
          border-style: solid;
          border-width: 1px;
          width: 50%;
          }
          #invoice-line-td {
          border-style: solid;
          border-width: 1px;
          }

          .budget-td {
          border-style: solid;
          border-width: 1px;
          border-color: black;
          border-spacing: 0;
          line-height: 18px;
          text-align: right;
          }
        </style>
        <script type="text/javascript">
          <![CDATA[var QRCode;!function(){function a(a){this.mode=c.MODE_8BIT_BYTE,this.data=a,this.parsedData=[];for(var b=[],d=0,e=this.data.length;e>d;d++){var f=this.data.charCodeAt(d);f>65536?(b[0]=240|(1835008&f)>>>18,b[1]=128|(258048&f)>>>12,b[2]=128|(4032&f)>>>6,b[3]=128|63&f):f>2048?(b[0]=224|(61440&f)>>>12,b[1]=128|(4032&f)>>>6,b[2]=128|63&f):f>128?(b[0]=192|(1984&f)>>>6,b[1]=128|63&f):b[0]=f,this.parsedData=this.parsedData.concat(b)}this.parsedData.length!=this.data.length&&(this.parsedData.unshift(191),this.parsedData.unshift(187),this.parsedData.unshift(239))}function b(a,b){this.typeNumber=a,this.errorCorrectLevel=b,this.modules=null,this.moduleCount=0,this.dataCache=null,this.dataList=[]}function i(a,b){if(void 0==a.length)throw new Error(a.length+"/"+b);for(var c=0;c<a.length&&0==a[c];)c++;this.num=new Array(a.length-c+b);for(var d=0;d<a.length-c;d++)this.num[d]=a[d+c]}function j(a,b){this.totalCount=a,this.dataCount=b}function k(){this.buffer=[],this.length=0}function m(){return"undefined"!=typeof CanvasRenderingContext2D}function n(){var a=!1,b=navigator.userAgent;return/android/i.test(b)&&(a=!0,aMat=b.toString().match(/android ([0-9]\.[0-9])/i),aMat&&aMat[1]&&(a=parseFloat(aMat[1]))),a}function r(a,b){for(var c=1,e=s(a),f=0,g=l.length;g>=f;f++){var h=0;switch(b){case d.L:h=l[f][0];break;case d.M:h=l[f][1];break;case d.Q:h=l[f][2];break;case d.H:h=l[f][3]}if(h>=e)break;c++}if(c>l.length)throw new Error("Too long data");return c}function s(a){var b=encodeURI(a).toString().replace(/\%[0-9a-fA-F]{2}/g,"a");return b.length+(b.length!=a?3:0)}a.prototype={getLength:function(){return this.parsedData.length},write:function(a){for(var b=0,c=this.parsedData.length;c>b;b++)a.put(this.parsedData[b],8)}},b.prototype={addData:function(b){var c=new a(b);this.dataList.push(c),this.dataCache=null},isDark:function(a,b){if(0>a||this.moduleCount<=a||0>b||this.moduleCount<=b)throw new Error(a+","+b);return this.modules[a][b]},getModuleCount:function(){return this.moduleCount},make:function(){this.makeImpl(!1,this.getBestMaskPattern())},makeImpl:function(a,c){this.moduleCount=4*this.typeNumber+17,this.modules=new Array(this.moduleCount);for(var d=0;d<this.moduleCount;d++){this.modules[d]=new Array(this.moduleCount);for(var e=0;e<this.moduleCount;e++)this.modules[d][e]=null}this.setupPositionProbePattern(0,0),this.setupPositionProbePattern(this.moduleCount-7,0),this.setupPositionProbePattern(0,this.moduleCount-7),this.setupPositionAdjustPattern(),this.setupTimingPattern(),this.setupTypeInfo(a,c),this.typeNumber>=7&&this.setupTypeNumber(a),null==this.dataCache&&(this.dataCache=b.createData(this.typeNumber,this.errorCorrectLevel,this.dataList)),this.mapData(this.dataCache,c)},setupPositionProbePattern:function(a,b){for(var c=-1;7>=c;c++)if(!(-1>=a+c||this.moduleCount<=a+c))for(var d=-1;7>=d;d++)-1>=b+d||this.moduleCount<=b+d||(this.modules[a+c][b+d]=c>=0&&6>=c&&(0==d||6==d)||d>=0&&6>=d&&(0==c||6==c)||c>=2&&4>=c&&d>=2&&4>=d?!0:!1)},getBestMaskPattern:function(){for(var a=0,b=0,c=0;8>c;c++){this.makeImpl(!0,c);var d=f.getLostPoint(this);(0==c||a>d)&&(a=d,b=c)}return b},createMovieClip:function(a,b,c){var d=a.createEmptyMovieClip(b,c),e=1;this.make();for(var f=0;f<this.modules.length;f++)for(var g=f*e,h=0;h<this.modules[f].length;h++){var i=h*e,j=this.modules[f][h];j&&(d.beginFill(0,100),d.moveTo(i,g),d.lineTo(i+e,g),d.lineTo(i+e,g+e),d.lineTo(i,g+e),d.endFill())}return d},setupTimingPattern:function(){for(var a=8;a<this.moduleCount-8;a++)null==this.modules[a][6]&&(this.modules[a][6]=0==a%2);for(var b=8;b<this.moduleCount-8;b++)null==this.modules[6][b]&&(this.modules[6][b]=0==b%2)},setupPositionAdjustPattern:function(){for(var a=f.getPatternPosition(this.typeNumber),b=0;b<a.length;b++)for(var c=0;c<a.length;c++){var d=a[b],e=a[c];if(null==this.modules[d][e])for(var g=-2;2>=g;g++)for(var h=-2;2>=h;h++)this.modules[d+g][e+h]=-2==g||2==g||-2==h||2==h||0==g&&0==h?!0:!1}},setupTypeNumber:function(a){for(var b=f.getBCHTypeNumber(this.typeNumber),c=0;18>c;c++){var d=!a&&1==(1&b>>c);this.modules[Math.floor(c/3)][c%3+this.moduleCount-8-3]=d}for(var c=0;18>c;c++){var d=!a&&1==(1&b>>c);this.modules[c%3+this.moduleCount-8-3][Math.floor(c/3)]=d}},setupTypeInfo:function(a,b){for(var c=this.errorCorrectLevel<<3|b,d=f.getBCHTypeInfo(c),e=0;15>e;e++){var g=!a&&1==(1&d>>e);6>e?this.modules[e][8]=g:8>e?this.modules[e+1][8]=g:this.modules[this.moduleCount-15+e][8]=g}for(var e=0;15>e;e++){var g=!a&&1==(1&d>>e);8>e?this.modules[8][this.moduleCount-e-1]=g:9>e?this.modules[8][15-e-1+1]=g:this.modules[8][15-e-1]=g}this.modules[this.moduleCount-8][8]=!a},mapData:function(a,b){for(var c=-1,d=this.moduleCount-1,e=7,g=0,h=this.moduleCount-1;h>0;h-=2)for(6==h&&h--;;){for(var i=0;2>i;i++)if(null==this.modules[d][h-i]){var j=!1;g<a.length&&(j=1==(1&a[g]>>>e));var k=f.getMask(b,d,h-i);k&&(j=!j),this.modules[d][h-i]=j,e--,-1==e&&(g++,e=7)}if(d+=c,0>d||this.moduleCount<=d){d-=c,c=-c;break}}}},b.PAD0=236,b.PAD1=17,b.createData=function(a,c,d){for(var e=j.getRSBlocks(a,c),g=new k,h=0;h<d.length;h++){var i=d[h];g.put(i.mode,4),g.put(i.getLength(),f.getLengthInBits(i.mode,a)),i.write(g)}for(var l=0,h=0;h<e.length;h++)l+=e[h].dataCount;if(g.getLengthInBits()>8*l)throw new Error("code length overflow. ("+g.getLengthInBits()+">"+8*l+")");for(g.getLengthInBits()+4<=8*l&&g.put(0,4);0!=g.getLengthInBits()%8;)g.putBit(!1);for(;;){if(g.getLengthInBits()>=8*l)break;if(g.put(b.PAD0,8),g.getLengthInBits()>=8*l)break;g.put(b.PAD1,8)}return b.createBytes(g,e)},b.createBytes=function(a,b){for(var c=0,d=0,e=0,g=new Array(b.length),h=new Array(b.length),j=0;j<b.length;j++){var k=b[j].dataCount,l=b[j].totalCount-k;d=Math.max(d,k),e=Math.max(e,l),g[j]=new Array(k);for(var m=0;m<g[j].length;m++)g[j][m]=255&a.buffer[m+c];c+=k;var n=f.getErrorCorrectPolynomial(l),o=new i(g[j],n.getLength()-1),p=o.mod(n);h[j]=new Array(n.getLength()-1);for(var m=0;m<h[j].length;m++){var q=m+p.getLength()-h[j].length;h[j][m]=q>=0?p.get(q):0}}for(var r=0,m=0;m<b.length;m++)r+=b[m].totalCount;for(var s=new Array(r),t=0,m=0;d>m;m++)for(var j=0;j<b.length;j++)m<g[j].length&&(s[t++]=g[j][m]);for(var m=0;e>m;m++)for(var j=0;j<b.length;j++)m<h[j].length&&(s[t++]=h[j][m]);return s};for(var c={MODE_NUMBER:1,MODE_ALPHA_NUM:2,MODE_8BIT_BYTE:4,MODE_KANJI:8},d={L:1,M:0,Q:3,H:2},e={PATTERN000:0,PATTERN001:1,PATTERN010:2,PATTERN011:3,PATTERN100:4,PATTERN101:5,PATTERN110:6,PATTERN111:7},f={PATTERN_POSITION_TABLE:[[],[6,18],[6,22],[6,26],[6,30],[6,34],[6,22,38],[6,24,42],[6,26,46],[6,28,50],[6,30,54],[6,32,58],[6,34,62],[6,26,46,66],[6,26,48,70],[6,26,50,74],[6,30,54,78],[6,30,56,82],[6,30,58,86],[6,34,62,90],[6,28,50,72,94],[6,26,50,74,98],[6,30,54,78,102],[6,28,54,80,106],[6,32,58,84,110],[6,30,58,86,114],[6,34,62,90,118],[6,26,50,74,98,122],[6,30,54,78,102,126],[6,26,52,78,104,130],[6,30,56,82,108,134],[6,34,60,86,112,138],[6,30,58,86,114,142],[6,34,62,90,118,146],[6,30,54,78,102,126,150],[6,24,50,76,102,128,154],[6,28,54,80,106,132,158],[6,32,58,84,110,136,162],[6,26,54,82,110,138,166],[6,30,58,86,114,142,170]],G15:1335,G18:7973,G15_MASK:21522,getBCHTypeInfo:function(a){for(var b=a<<10;f.getBCHDigit(b)-f.getBCHDigit(f.G15)>=0;)b^=f.G15<<f.getBCHDigit(b)-f.getBCHDigit(f.G15);return(a<<10|b)^f.G15_MASK},getBCHTypeNumber:function(a){for(var b=a<<12;f.getBCHDigit(b)-f.getBCHDigit(f.G18)>=0;)b^=f.G18<<f.getBCHDigit(b)-f.getBCHDigit(f.G18);return a<<12|b},getBCHDigit:function(a){for(var b=0;0!=a;)b++,a>>>=1;return b},getPatternPosition:function(a){return f.PATTERN_POSITION_TABLE[a-1]},getMask:function(a,b,c){switch(a){case e.PATTERN000:return 0==(b+c)%2;case e.PATTERN001:return 0==b%2;case e.PATTERN010:return 0==c%3;case e.PATTERN011:return 0==(b+c)%3;case e.PATTERN100:return 0==(Math.floor(b/2)+Math.floor(c/3))%2;case e.PATTERN101:return 0==b*c%2+b*c%3;case e.PATTERN110:return 0==(b*c%2+b*c%3)%2;case e.PATTERN111:return 0==(b*c%3+(b+c)%2)%2;default:throw new Error("bad maskPattern:"+a)}},getErrorCorrectPolynomial:function(a){for(var b=new i([1],0),c=0;a>c;c++)b=b.multiply(new i([1,g.gexp(c)],0));return b},getLengthInBits:function(a,b){if(b>=1&&10>b)switch(a){case c.MODE_NUMBER:return 10;case c.MODE_ALPHA_NUM:return 9;case c.MODE_8BIT_BYTE:return 8;case c.MODE_KANJI:return 8;default:throw new Error("mode:"+a)}else if(27>b)switch(a){case c.MODE_NUMBER:return 12;case c.MODE_ALPHA_NUM:return 11;case c.MODE_8BIT_BYTE:return 16;case c.MODE_KANJI:return 10;default:throw new Error("mode:"+a)}else{if(!(41>b))throw new Error("type:"+b);switch(a){case c.MODE_NUMBER:return 14;case c.MODE_ALPHA_NUM:return 13;case c.MODE_8BIT_BYTE:return 16;case c.MODE_KANJI:return 12;default:throw new Error("mode:"+a)}}},getLostPoint:function(a){for(var b=a.getModuleCount(),c=0,d=0;b>d;d++)for(var e=0;b>e;e++){for(var f=0,g=a.isDark(d,e),h=-1;1>=h;h++)if(!(0>d+h||d+h>=b))for(var i=-1;1>=i;i++)0>e+i||e+i>=b||(0!=h||0!=i)&&g==a.isDark(d+h,e+i)&&f++;f>5&&(c+=3+f-5)}for(var d=0;b-1>d;d++)for(var e=0;b-1>e;e++){var j=0;a.isDark(d,e)&&j++,a.isDark(d+1,e)&&j++,a.isDark(d,e+1)&&j++,a.isDark(d+1,e+1)&&j++,(0==j||4==j)&&(c+=3)}for(var d=0;b>d;d++)for(var e=0;b-6>e;e++)a.isDark(d,e)&&!a.isDark(d,e+1)&&a.isDark(d,e+2)&&a.isDark(d,e+3)&&a.isDark(d,e+4)&&!a.isDark(d,e+5)&&a.isDark(d,e+6)&&(c+=40);for(var e=0;b>e;e++)for(var d=0;b-6>d;d++)a.isDark(d,e)&&!a.isDark(d+1,e)&&a.isDark(d+2,e)&&a.isDark(d+3,e)&&a.isDark(d+4,e)&&!a.isDark(d+5,e)&&a.isDark(d+6,e)&&(c+=40);for(var k=0,e=0;b>e;e++)for(var d=0;b>d;d++)a.isDark(d,e)&&k++;var l=Math.abs(100*k/b/b-50)/5;return c+=10*l}},g={glog:function(a){if(1>a)throw new Error("glog("+a+")");return g.LOG_TABLE[a]},gexp:function(a){for(;0>a;)a+=255;for(;a>=256;)a-=255;return g.EXP_TABLE[a]},EXP_TABLE:new Array(256),LOG_TABLE:new Array(256)},h=0;8>h;h++)g.EXP_TABLE[h]=1<<h;for(var h=8;256>h;h++)g.EXP_TABLE[h]=g.EXP_TABLE[h-4]^g.EXP_TABLE[h-5]^g.EXP_TABLE[h-6]^g.EXP_TABLE[h-8];for(var h=0;255>h;h++)g.LOG_TABLE[g.EXP_TABLE[h]]=h;i.prototype={get:function(a){return this.num[a]},getLength:function(){return this.num.length},multiply:function(a){for(var b=new Array(this.getLength()+a.getLength()-1),c=0;c<this.getLength();c++)for(var d=0;d<a.getLength();d++)b[c+d]^=g.gexp(g.glog(this.get(c))+g.glog(a.get(d)));return new i(b,0)},mod:function(a){if(this.getLength()-a.getLength()<0)return this;for(var b=g.glog(this.get(0))-g.glog(a.get(0)),c=new Array(this.getLength()),d=0;d<this.getLength();d++)c[d]=this.get(d);for(var d=0;d<a.getLength();d++)c[d]^=g.gexp(g.glog(a.get(d))+b);return new i(c,0).mod(a)}},j.RS_BLOCK_TABLE=[[1,26,19],[1,26,16],[1,26,13],[1,26,9],[1,44,34],[1,44,28],[1,44,22],[1,44,16],[1,70,55],[1,70,44],[2,35,17],[2,35,13],[1,100,80],[2,50,32],[2,50,24],[4,25,9],[1,134,108],[2,67,43],[2,33,15,2,34,16],[2,33,11,2,34,12],[2,86,68],[4,43,27],[4,43,19],[4,43,15],[2,98,78],[4,49,31],[2,32,14,4,33,15],[4,39,13,1,40,14],[2,121,97],[2,60,38,2,61,39],[4,40,18,2,41,19],[4,40,14,2,41,15],[2,146,116],[3,58,36,2,59,37],[4,36,16,4,37,17],[4,36,12,4,37,13],[2,86,68,2,87,69],[4,69,43,1,70,44],[6,43,19,2,44,20],[6,43,15,2,44,16],[4,101,81],[1,80,50,4,81,51],[4,50,22,4,51,23],[3,36,12,8,37,13],[2,116,92,2,117,93],[6,58,36,2,59,37],[4,46,20,6,47,21],[7,42,14,4,43,15],[4,133,107],[8,59,37,1,60,38],[8,44,20,4,45,21],[12,33,11,4,34,12],[3,145,115,1,146,116],[4,64,40,5,65,41],[11,36,16,5,37,17],[11,36,12,5,37,13],[5,109,87,1,110,88],[5,65,41,5,66,42],[5,54,24,7,55,25],[11,36,12],[5,122,98,1,123,99],[7,73,45,3,74,46],[15,43,19,2,44,20],[3,45,15,13,46,16],[1,135,107,5,136,108],[10,74,46,1,75,47],[1,50,22,15,51,23],[2,42,14,17,43,15],[5,150,120,1,151,121],[9,69,43,4,70,44],[17,50,22,1,51,23],[2,42,14,19,43,15],[3,141,113,4,142,114],[3,70,44,11,71,45],[17,47,21,4,48,22],[9,39,13,16,40,14],[3,135,107,5,136,108],[3,67,41,13,68,42],[15,54,24,5,55,25],[15,43,15,10,44,16],[4,144,116,4,145,117],[17,68,42],[17,50,22,6,51,23],[19,46,16,6,47,17],[2,139,111,7,140,112],[17,74,46],[7,54,24,16,55,25],[34,37,13],[4,151,121,5,152,122],[4,75,47,14,76,48],[11,54,24,14,55,25],[16,45,15,14,46,16],[6,147,117,4,148,118],[6,73,45,14,74,46],[11,54,24,16,55,25],[30,46,16,2,47,17],[8,132,106,4,133,107],[8,75,47,13,76,48],[7,54,24,22,55,25],[22,45,15,13,46,16],[10,142,114,2,143,115],[19,74,46,4,75,47],[28,50,22,6,51,23],[33,46,16,4,47,17],[8,152,122,4,153,123],[22,73,45,3,74,46],[8,53,23,26,54,24],[12,45,15,28,46,16],[3,147,117,10,148,118],[3,73,45,23,74,46],[4,54,24,31,55,25],[11,45,15,31,46,16],[7,146,116,7,147,117],[21,73,45,7,74,46],[1,53,23,37,54,24],[19,45,15,26,46,16],[5,145,115,10,146,116],[19,75,47,10,76,48],[15,54,24,25,55,25],[23,45,15,25,46,16],[13,145,115,3,146,116],[2,74,46,29,75,47],[42,54,24,1,55,25],[23,45,15,28,46,16],[17,145,115],[10,74,46,23,75,47],[10,54,24,35,55,25],[19,45,15,35,46,16],[17,145,115,1,146,116],[14,74,46,21,75,47],[29,54,24,19,55,25],[11,45,15,46,46,16],[13,145,115,6,146,116],[14,74,46,23,75,47],[44,54,24,7,55,25],[59,46,16,1,47,17],[12,151,121,7,152,122],[12,75,47,26,76,48],[39,54,24,14,55,25],[22,45,15,41,46,16],[6,151,121,14,152,122],[6,75,47,34,76,48],[46,54,24,10,55,25],[2,45,15,64,46,16],[17,152,122,4,153,123],[29,74,46,14,75,47],[49,54,24,10,55,25],[24,45,15,46,46,16],[4,152,122,18,153,123],[13,74,46,32,75,47],[48,54,24,14,55,25],[42,45,15,32,46,16],[20,147,117,4,148,118],[40,75,47,7,76,48],[43,54,24,22,55,25],[10,45,15,67,46,16],[19,148,118,6,149,119],[18,75,47,31,76,48],[34,54,24,34,55,25],[20,45,15,61,46,16]],j.getRSBlocks=function(a,b){var c=j.getRsBlockTable(a,b);if(void 0==c)throw new Error("bad rs block @ typeNumber:"+a+"/errorCorrectLevel:"+b);for(var d=c.length/3,e=[],f=0;d>f;f++)for(var g=c[3*f+0],h=c[3*f+1],i=c[3*f+2],k=0;g>k;k++)e.push(new j(h,i));return e},j.getRsBlockTable=function(a,b){switch(b){case d.L:return j.RS_BLOCK_TABLE[4*(a-1)+0];case d.M:return j.RS_BLOCK_TABLE[4*(a-1)+1];case d.Q:return j.RS_BLOCK_TABLE[4*(a-1)+2];case d.H:return j.RS_BLOCK_TABLE[4*(a-1)+3];default:return void 0}},k.prototype={get:function(a){var b=Math.floor(a/8);return 1==(1&this.buffer[b]>>>7-a%8)},put:function(a,b){for(var c=0;b>c;c++)this.putBit(1==(1&a>>>b-c-1))},getLengthInBits:function(){return this.length},putBit:function(a){var b=Math.floor(this.length/8);this.buffer.length<=b&&this.buffer.push(0),a&&(this.buffer[b]|=128>>>this.length%8),this.length++}};var l=[[17,14,11,7],[32,26,20,14],[53,42,32,24],[78,62,46,34],[106,84,60,44],[134,106,74,58],[154,122,86,64],[192,152,108,84],[230,180,130,98],[271,213,151,119],[321,251,177,137],[367,287,203,155],[425,331,241,177],[458,362,258,194],[520,412,292,220],[586,450,322,250],[644,504,364,280],[718,560,394,310],[792,624,442,338],[858,666,482,382],[929,711,509,403],[1003,779,565,439],[1091,857,611,461],[1171,911,661,511],[1273,997,715,535],[1367,1059,751,593],[1465,1125,805,625],[1528,1190,868,658],[1628,1264,908,698],[1732,1370,982,742],[1840,1452,1030,790],[1952,1538,1112,842],[2068,1628,1168,898],[2188,1722,1228,958],[2303,1809,1283,983],[2431,1911,1351,1051],[2563,1989,1423,1093],[2699,2099,1499,1139],[2809,2213,1579,1219],[2953,2331,1663,1273]],o=function(){var a=function(a,b){this._el=a,this._htOption=b};return a.prototype.draw=function(a){function g(a,b){var c=document.createElementNS("http://www.w3.org/2000/svg",a);for(var d in b)b.hasOwnProperty(d)&&c.setAttribute(d,b[d]);return c}var b=this._htOption,c=this._el,d=a.getModuleCount();Math.floor(b.width/d),Math.floor(b.height/d),this.clear();var h=g("svg",{viewBox:"0 0 "+String(d)+" "+String(d),width:"100%",height:"100%",fill:b.colorLight});h.setAttributeNS("http://www.w3.org/2000/xmlns/","xmlns:xlink","http://www.w3.org/1999/xlink"),c.appendChild(h),h.appendChild(g("rect",{fill:b.colorDark,width:"1",height:"1",id:"template"}));for(var i=0;d>i;i++)for(var j=0;d>j;j++)if(a.isDark(i,j)){var k=g("use",{x:String(i),y:String(j)});k.setAttributeNS("http://www.w3.org/1999/xlink","href","#template"),h.appendChild(k)}},a.prototype.clear=function(){for(;this._el.hasChildNodes();)this._el.removeChild(this._el.lastChild)},a}(),p="svg"===document.documentElement.tagName.toLowerCase(),q=p?o:m()?function(){function a(){this._elImage.src=this._elCanvas.toDataURL("image/png"),this._elImage.style.display="block",this._elCanvas.style.display="none"}function d(a,b){var c=this;if(c._fFail=b,c._fSuccess=a,null===c._bSupportDataURI){var d=document.createElement("img"),e=function(){c._bSupportDataURI=!1,c._fFail&&_fFail.call(c)},f=function(){c._bSupportDataURI=!0,c._fSuccess&&c._fSuccess.call(c)};return d.onabort=e,d.onerror=e,d.onload=f,d.src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",void 0}c._bSupportDataURI===!0&&c._fSuccess?c._fSuccess.call(c):c._bSupportDataURI===!1&&c._fFail&&c._fFail.call(c)}if(this._android&&this._android<=2.1){var b=1/window.devicePixelRatio,c=CanvasRenderingContext2D.prototype.drawImage;CanvasRenderingContext2D.prototype.drawImage=function(a,d,e,f,g,h,i,j){if("nodeName"in a&&/img/i.test(a.nodeName))for(var l=arguments.length-1;l>=1;l--)arguments[l]=arguments[l]*b;else"undefined"==typeof j&&(arguments[1]*=b,arguments[2]*=b,arguments[3]*=b,arguments[4]*=b);c.apply(this,arguments)}}var e=function(a,b){this._bIsPainted=!1,this._android=n(),this._htOption=b,this._elCanvas=document.createElement("canvas"),this._elCanvas.width=b.width,this._elCanvas.height=b.height,a.appendChild(this._elCanvas),this._el=a,this._oContext=this._elCanvas.getContext("2d"),this._bIsPainted=!1,this._elImage=document.createElement("img"),this._elImage.style.display="none",this._el.appendChild(this._elImage),this._bSupportDataURI=null};return e.prototype.draw=function(a){var b=this._elImage,c=this._oContext,d=this._htOption,e=a.getModuleCount(),f=d.width/e,g=d.height/e,h=Math.round(f),i=Math.round(g);b.style.display="none",this.clear();for(var j=0;e>j;j++)for(var k=0;e>k;k++){var l=a.isDark(j,k),m=k*f,n=j*g;c.strokeStyle=l?d.colorDark:d.colorLight,c.lineWidth=1,c.fillStyle=l?d.colorDark:d.colorLight,c.fillRect(m,n,f,g),c.strokeRect(Math.floor(m)+.5,Math.floor(n)+.5,h,i),c.strokeRect(Math.ceil(m)-.5,Math.ceil(n)-.5,h,i)}this._bIsPainted=!0},e.prototype.makeImage=function(){this._bIsPainted&&d.call(this,a)},e.prototype.isPainted=function(){return this._bIsPainted},e.prototype.clear=function(){this._oContext.clearRect(0,0,this._elCanvas.width,this._elCanvas.height),this._bIsPainted=!1},e.prototype.round=function(a){return a?Math.floor(1e3*a)/1e3:a},e}():function(){var a=function(a,b){this._el=a,this._htOption=b};return a.prototype.draw=function(a){for(var b=this._htOption,c=this._el,d=a.getModuleCount(),e=Math.floor(b.width/d),f=Math.floor(b.height/d),g=['<table style="border:0;border-collapse:collapse;">'],h=0;d>h;h++){g.push("<tr>");for(var i=0;d>i;i++)g.push('<td style="border:0;border-collapse:collapse;padding:0;margin:0;width:'+e+"px;height:"+f+"px;background-color:"+(a.isDark(h,i)?b.colorDark:b.colorLight)+';"></td>');g.push("</tr>")}g.push("</table>"),c.innerHTML=g.join("");var j=c.childNodes[0],k=(b.width-j.offsetWidth)/2,l=(b.height-j.offsetHeight)/2;k>0&&l>0&&(j.style.margin=l+"px "+k+"px")},a.prototype.clear=function(){this._el.innerHTML=""},a}();QRCode=function(a,b){if(this._htOption={width:256,height:256,typeNumber:4,colorDark:"#000000",colorLight:"#ffffff",correctLevel:d.H},"string"==typeof b&&(b={text:b}),b)for(var c in b)this._htOption[c]=b[c];"string"==typeof a&&(a=document.getElementById(a)),this._android=n(),this._el=a,this._oQRCode=null,this._oDrawing=new q(this._el,this._htOption),this._htOption.text&&this.makeCode(this._htOption.text)},QRCode.prototype.makeCode=function(a){this._oQRCode=new b(r(a,this._htOption.correctLevel),this._htOption.correctLevel),this._oQRCode.addData(a),this._oQRCode.make(),this._el.title=a,this._oDrawing.draw(this._oQRCode),this.makeImage()},QRCode.prototype.makeImage=function(){"function"==typeof this._oDrawing.makeImage&&(!this._android||this._android>=3)&&this._oDrawing.makeImage()},QRCode.prototype.clear=function(){this._oDrawing.clear()},QRCode.CorrectLevel=d}();]]>
        </script>
        <title>e-İrsaliye</title>
      </head>


      <body style="width:800px">
        <xsl:for-each select="$XML">

          <!-- GONDERICI - BELGE LOGO - FIRMA LOGO TABLOSU -->
          <table style="width: 100%;">
            <tbody>
              <tr>
                <td style="width: 40%;">
                  <hr/>

                  <!-- GONDERICI TABLOSU -->
                  <table style="width: 100%;">
                    <tbody>
                      <tr align="left">
                        <xsl:for-each
                                select="n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party">
                          <td align="left">
                            <xsl:if test="cac:PartyName">
                              <xsl:value-of select="cac:PartyName/cbc:Name"/>
                              <br/>
                            </xsl:if>
                            <xsl:for-each select="cac:Person">
                              <xsl:for-each select="cbc:Title">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:FirstName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:MiddleName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:FamilyName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:NameSuffix">
                                <xsl:apply-templates/>
                              </xsl:for-each>
                            </xsl:for-each>
                          </td>
                        </xsl:for-each>
                      </tr>

                      <tr>
                        <xsl:for-each
                                select="n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party">
                          <td align="left">
                            <xsl:for-each select="cac:PostalAddress">
                              <xsl:for-each select="cbc:StreetName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:BuildingName">
                                <xsl:apply-templates/>
                              </xsl:for-each>
                              <xsl:if test="cbc:BuildingNumber">
                                <xsl:text> No:</xsl:text>
                                <xsl:for-each select="cbc:BuildingNumber">
                                  <xsl:apply-templates/>
                                </xsl:for-each>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:if>
                              <br/>
                              <xsl:for-each select="cbc:PostalZone">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                              <xsl:for-each select="cbc:CitySubdivisionName">
                                <xsl:apply-templates/>
                              </xsl:for-each>
                              <xsl:text>/ </xsl:text>
                              <xsl:for-each select="cbc:CityName">
                                <xsl:apply-templates/>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                            </xsl:for-each>
                          </td>
                        </xsl:for-each>
                      </tr>

                      <xsl:if
                              test="//n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party/cac:Contact/cbc:Telephone or //n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party/cac:Contact/cbc:Telefax">
                        <tr align="left">
                          <xsl:for-each
                                  select="n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party">
                            <td align="left">
                              <xsl:for-each select="cac:Contact">
                                <xsl:if test="cbc:Telephone">
                                  <xsl:text>Tel: </xsl:text>
                                  <xsl:for-each select="cbc:Telephone">
                                    <xsl:apply-templates/>
                                  </xsl:for-each>
                                </xsl:if>
                                <xsl:if test="cbc:Telefax">
                                  <xsl:text> Fax: </xsl:text>
                                  <xsl:for-each select="cbc:Telefax">
                                    <xsl:apply-templates/>
                                  </xsl:for-each>
                                </xsl:if>
                                <xsl:text>&#160;</xsl:text>
                              </xsl:for-each>
                            </td>
                          </xsl:for-each>
                        </tr>
                      </xsl:if>

                      <xsl:for-each
                              select="//n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party/cbc:WebsiteURI">
                        <tr align="left">
                          <td>
                            <xsl:text>Web Sitesi: </xsl:text>
                            <xsl:value-of select="."/>
                          </td>
                        </tr>
                      </xsl:for-each>

                      <xsl:for-each
                              select="//n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party/cac:Contact/cbc:ElectronicMail">
                        <tr align="left">
                          <td>
                            <xsl:text>E-Posta: </xsl:text>
                            <xsl:value-of select="."/>
                          </td>
                        </tr>
                      </xsl:for-each>

                      <tr align="left">
                        <xsl:for-each
                                select="n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party">
                          <td align="left">
                            <xsl:text>Vergi Dairesi: </xsl:text>
                            <xsl:for-each select="cac:PartyTaxScheme">
                              <xsl:for-each select="cac:TaxScheme">
                                <xsl:for-each select="cbc:Name">
                                  <xsl:apply-templates/>
                                </xsl:for-each>
                              </xsl:for-each>
                              <xsl:text>&#160; </xsl:text>
                            </xsl:for-each>
                          </td>
                        </xsl:for-each>
                      </tr>

                      <xsl:for-each
                              select="//n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party/cac:PartyIdentification">
                        <tr align="left">
                          <td>
                            <xsl:value-of select="cbc:ID/@schemeID"/>
                            <xsl:text>: </xsl:text>
                            <xsl:value-of select="cbc:ID"/>
                          </td>
                        </tr>
                      </xsl:for-each>
                    </tbody>
                  </table>
                  <hr/>
                </td>


                <!-- BELGE LOGO -->
                <td style="width: 20%; text-align: center;">

                  <img style="width:auto;" align="middle" alt="E-Fatura Logo"
                       src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="/>

                  <h1 style="text-align: center;">
                    <span style="font-weight:bold; ">
                      <xsl:text>e-İRSALİYE</xsl:text>
                    </span>
                  </h1>

                </td>

                <!-- FIRMA LOGOSU -->
                <td style="width: 40%; text-align: center;">
                                  <div id="qrcode" style="display: flex; flex-direction: column; align-items: center;"/>
                </td>
              </tr>
            </tbody>
          </table>



          <!-- ALICI - IMZA - FATURA BILGILERI TABLOSU -->
          <table style="width: 100%">
            <tbody>
              <tr>
                <td style="width: 40%;">
                  <hr/>
                  <table style="width: 100%;">
                    <tbody>
                      <tr>
                        <xsl:for-each
                                select="n1:DespatchAdvice/cac:DeliveryCustomerParty/cac:Party">
                          <td style="width:469px; " align="left">
                            <span style="font-weight:bold; ">
                              <xsl:text>SAYIN</xsl:text>
                            </span>
                          </td>
                        </xsl:for-each>
                      </tr>

                      <tr>
                        <xsl:choose>
                          <xsl:when
                                  test="n1:DespatchAdvice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='TAXFREE']">
                            <xsl:for-each
                                    select="n1:DespatchAdvice/cac:BuyerCustomerParty/cac:Party">
                              <xsl:call-template name="Party_Title">
                                <xsl:with-param name="PartyType">
                                  TAXFREE
                                </xsl:with-param>
                              </xsl:call-template>
                            </xsl:for-each>
                          </xsl:when>
                          <xsl:otherwise>
                            <xsl:for-each
                                    select="n1:DespatchAdvice/cac:DeliveryCustomerParty/cac:Party">
                              <xsl:call-template name="Party_Title">
                                <xsl:with-param name="PartyType">OTHER
                                </xsl:with-param>
                              </xsl:call-template>
                            </xsl:for-each>
                          </xsl:otherwise>
                        </xsl:choose>
                      </tr>

                      <xsl:choose>
                        <xsl:when
                                test="n1:DespatchAdvice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='TAXFREE']">
                          <xsl:for-each
                                  select="n1:DespatchAdvice/cac:BuyerCustomerParty/cac:Party">
                            <tr>
                              <xsl:call-template name="Party_Adress">
                                <xsl:with-param name="PartyType">
                                  TAXFREE
                                </xsl:with-param>
                              </xsl:call-template>
                            </tr>
                            <xsl:call-template name="Party_Other">
                              <xsl:with-param name="PartyType">TAXFREE
                              </xsl:with-param>
                            </xsl:call-template>
                          </xsl:for-each>
                        </xsl:when>
                        <xsl:otherwise>
                          <xsl:for-each
                                  select="n1:DespatchAdvice/cac:DeliveryCustomerParty/cac:Party">
                            <tr>
                              <xsl:call-template name="Party_Adress">
                                <xsl:with-param name="PartyType">OTHER
                                </xsl:with-param>
                              </xsl:call-template>
                            </tr>
                            <xsl:call-template name="Party_Other">
                              <xsl:with-param name="PartyType">OTHER
                              </xsl:with-param>
                            </xsl:call-template>
                          </xsl:for-each>
                        </xsl:otherwise>
                      </xsl:choose>
                    </tbody>
                  </table>
                  <hr/>
                </td>

                <td style="width: 30%; text-align: center;">
                  <!-- SIGN_REF -->
                </td>

                <td style="width: 30%;">
                  <table style="width:100%; border-collapse: collapse;">
                    <tbody>
                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>Özelleştirme No:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each select="n1:DespatchAdvice/cbc:CustomizationID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>Senaryo:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each select="n1:DespatchAdvice/cbc:ProfileID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>İrsaliye Tipi:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each select="n1:DespatchAdvice/cbc:DespatchAdviceTypeCode">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>İrsaliye No:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each select="n1:DespatchAdvice/cbc:ID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>İrsaliye Tarihi:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each select="n1:DespatchAdvice/cbc:IssueDate">
                            <xsl:apply-templates select="."/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>İrsaliye Zamanı:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each select="n1:DespatchAdvice/cbc:IssueTime">
                            <xsl:apply-templates select="."/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>Sevk Tarihi:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each
                                  select="n1:DespatchAdvice/cac:Shipment/cac:Delivery/cac:Despatch/cbc:ActualDespatchDate">
                            <xsl:apply-templates select="."/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>Sevk Zamanı:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each
                                  select="n1:DespatchAdvice/cac:Shipment/cac:Delivery/cac:Despatch/cbc:ActualDespatchTime">
                            <xsl:apply-templates select="."/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>Sipariş No:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each
                                  select="n1:DespatchAdvice/cac:OrderReference/cbc:ID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                        </td>
                      </tr>

                      <tr>
                        <td id="invoice-info-td" style="width: 50%;">
                          <span style="font-weight:bold; ">
                            <xsl:text>Sipariş Tarihi:</xsl:text>
                          </span>
                        </td>
                        <td id="invoice-info-td" style="width: 50%;">
                          <xsl:for-each
                                  select="n1:DespatchAdvice/cac:OrderReference/cbc:IssueDate">
                            <xsl:apply-templates select="."/>
                          </xsl:for-each>
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </td>
              </tr>
            </tbody>
          </table>

          <!-- ETTN SATIRI -->
          <table style="width: 40%; margin-bottom: 5px;">
            <tbody>
              <tr>
                <td>
                  <span style="font-weight:bold; ">
                    <xsl:text>ETTN:&#160;</xsl:text>
                  </span>
                  <xsl:for-each select="n1:DespatchAdvice/cbc:UUID">
                    <xsl:apply-templates/>
                  </xsl:for-each>
                </td>
              </tr>
            </tbody>
          </table>


          <!-- URUNLER SATIRI (TABLOSU) -->
          <table style="width: 100%; border-collapse: collapse; border-style: solid; border-width: 2px;">
            <tbody>
              <tr>
                <td id="invoice-line-td" style="width:5%; text-align: center; background-color: lightgray;">
                  <span style="font-weight:bold;">
                    <xsl:text>Sıra No</xsl:text>
                  </span>
                </td>

                <td id="invoice-line-td" style="width:30%; text-align: center; background-color: lightgray;">
                  <span style="font-weight:bold;">
                    <xsl:text>Mal</xsl:text>
                  </span>
                </td>


                <xsl:choose>
                  <xsl:when test="$varStockCode &gt; 0">
                    <td id="invoice-line-td" style="width:10%; text-align: center; background-color: lightgray;">
                      <span style="font-weight:bold;">
                        <xsl:text>Stok Kodu</xsl:text>
                      </span>
                    </td>
                  </xsl:when>
                  <xsl:otherwise>
                  </xsl:otherwise>
                </xsl:choose>


                <xsl:choose>
                  <xsl:when test="$varManufacturerItemId &gt; 0">
                    <td id="invoice-line-td" style="width:10%; text-align: center; background-color: lightgray;">
                      <span style="font-weight:bold;">
                        <xsl:text>Barkod</xsl:text>
                      </span>
                    </td>
                  </xsl:when>
                  <xsl:otherwise>
                  </xsl:otherwise>
                </xsl:choose>

                <xsl:choose>
                  <xsl:when test="$varBuyersItemId &gt; 0">
                    <td id="invoice-line-td" style="width:10%; text-align: center; background-color: lightgray;">
                      <span style="font-weight:bold;">
                        <xsl:text>Müşteri Ürün Kodu</xsl:text>
                      </span>
                    </td>
                  </xsl:when>
                  <xsl:otherwise>
                  </xsl:otherwise>
                </xsl:choose>



                <td id="invoice-line-td" style="width:10%; text-align: center; background-color: lightgray;">
                  <span style="font-weight:bold;">
                    <xsl:text>Miktar</xsl:text>
                  </span>
                </td>

                <td id="invoice-line-td" style="width:10%; text-align: center; background-color: lightgray;">
                  <span style="font-weight:bold;">
                    <xsl:text>Birim Fiyat</xsl:text>
                  </span>
                </td>

                <td id="invoice-line-td" style="width:15%; text-align: center; background-color: lightgray;">
                  <span style="font-weight:bold;">
                    <xsl:text>Sonradan Gönd. Miktar</xsl:text>
                  </span>
                </td>
                  <xsl:choose>
                      <xsl:when test="$varKunyeNo">
                          <td id="invoice-line-td" style="width:15%; text-align: center; background-color: lightgray;">
                              <span style="font-weight:bold;">
                                  <xsl:text>Künye No</xsl:text>
                              </span>
                          </td>
                      </xsl:when>
                      <xsl:otherwise>
                      </xsl:otherwise>
                  </xsl:choose>
                <xsl:choose>
                  <xsl:when test="$varLineDescription &gt; 0">
                    <td id="invoice-line-td" style="width:15%; text-align: center; background-color: lightgray;">
                      <span style="font-weight:bold;">
                        <xsl:text>Satır Açıklaması</xsl:text>
                      </span>
                    </td>
                  </xsl:when>
                  <xsl:otherwise>
                  </xsl:otherwise>
                </xsl:choose>

                <td id="invoice-line-td" style="width:15%; text-align: center; background-color: lightgray;">
                  <span style="font-weight:bold;">
                    <xsl:text>Tutar</xsl:text>
                  </span>
                </td>
              </tr>

              <xsl:if test="count(//n1:DespatchAdvice/cac:DespatchLine) &gt;= 1">
                <xsl:for-each select="//n1:DespatchAdvice/cac:DespatchLine">
                  <xsl:apply-templates select="."/>
                </xsl:for-each>
              </xsl:if>
              <xsl:if test="count(//n1:DespatchAdvice/cac:DespatchLine) &lt; 1">
                <xsl:choose>
                  <xsl:when test="//n1:DespatchAdvice/cac:DespatchLine[1]">
                    <xsl:apply-templates
                            select="//n1:DespatchAdvice/cac:DespatchLine[1]"/>
                  </xsl:when>
                  <xsl:otherwise>
                    <xsl:apply-templates select="//n1:DespatchAdvice"/>
                  </xsl:otherwise>
                </xsl:choose>
              </xsl:if>
            </tbody>
          </table>
        </xsl:for-each>

        <table style="margin-bottom: 10px; width: 100%; margin-top: 20px;" cellspacing="0" cellpadding="0">
          <tr>
            <td style="width: 60%;"/>

            <td style="width: 40%;">
              <table style="width: 100%; border-collapse: collapse;" cellspacing="0" cellpadding="0">
                <tr>
                  <td class="budget-td" style="width: 50%;">
                    <span style="font-weight:bold; ">
                      <xsl:text>Toplam Tutar</xsl:text>
                    </span>
                  </td>

                  <td class="budget-td" style="width: 50%;">
                      <xsl:if test="n1:DespatchAdvice/cac:Shipment/cac:GoodsItem/cbc:ValueAmount != 0">
                          <xsl:for-each select="n1:DespatchAdvice/cac:Shipment/cac:GoodsItem/cbc:ValueAmount">
                            <xsl:call-template name="Curr_Type"/>
                          </xsl:for-each>
                      </xsl:if>
                  </td>
                </tr>
              </table>
            </td>
          </tr>
        </table>

        <xsl:if test="//n1:DespatchAdvice/cac:AdditionalDocumentReference">
          <table style="margin-bottom: 10px; width: 100%; margin-top: 20px; border-collapse: collapse;" cellspacing="0" cellpadding="0">
            <thead>
              <tr>
                <td align="left">
                  <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;İlgili
                    Dokümanlar
                  </span>
                </td>
                <td align="left">
                  <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;
                  </span>
                </td>
                <td align="left">
                  <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;
                  </span>
                </td>
                <td align="left">
                  <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;
                  </span>
                </td>
              </tr>
            </thead>
            <tbody>
              <tr align="left" class="lineTableTr">
                <td class="lineTableTd">
                  <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;Doküman
                    No
                  </span>
                </td>
                <td class="lineTableTd">
                  <span style="font-weight:bold; " align="center">
                    &#160;&#160;&#160;&#160;&#160;Tarih
                  </span>
                </td>
                <td class="lineTableTd">
                  <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;Doküman
                    Tipi
                  </span>
                </td>
                <td class="lineTableTd">
                  <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;Açıklama</span>
                </td>
              </tr>
              <xsl:for-each select="//n1:DespatchAdvice/cac:AdditionalDocumentReference">
                <tr align="left" class="lineTableTr">
                  <td class="lineTableTd">&#160;&#160;&#160;&#160;&#160;
                    <xsl:value-of select="./cbc:ID"/>
                  </td>
                  <td class="lineTableTd">&#160;&#160;&#160;&#160;&#160;
                    <xsl:for-each select="./cbc:IssueDate">
                      <xsl:apply-templates select="."/>
                    </xsl:for-each>
                  </td>
                  <td class="lineTableTd">&#160;&#160;&#160;&#160;&#160;
                    <xsl:value-of select="./cbc:DocumentType"/>
                  </td>
                  <td class="lineTableTd">&#160;&#160;&#160;&#160;&#160;
                    <xsl:value-of select="./cbc:DocumentDescription"/>
                  </td>
                </tr>
              </xsl:for-each>
            </tbody>
          </table>
        </xsl:if>


        <table style="margin-bottom: 10px; width: 100%; margin-top: 20px; border: 2px solid black; min-height: 120px;" cellspacing="0" cellpadding="0">

          <tbody>
            <tr>
              <td style="width: 30%">
                <table style="width: 100%;" cellspacing="0" cellpadding="0">
                  <tr>
                    <td style="width: 100%">
                      <span style="font-weight:bold;">
                        Açıklamalar
                      </span>
                    </td>
                  </tr>

                    <tr>
                        <td>
                            <span>
                                <!-- NOTE_REF -->
                            </span>
                        </td>
                    </tr>

                  <xsl:for-each select="//n1:DespatchAdvice/cbc:Note">
                    <tr>
                      <td style="width: 100%">
                        <b>Not:</b>
                        <xsl:value-of select="."/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each select="//cac:SellerSupplierParty">
                    <tr>
                      <td style="width: 100%">
                        <b>Asıl Satıcı VKN/TCKN:</b>
                        <xsl:value-of select="cac:Party/cac:PartyIdentification/cbc:ID"/>
                      </td>
                    </tr>

                    <tr>
                      <td style="width: 100%">
                        <b>Asıl Satıcı Ünvan:</b>
                        <xsl:value-of select="cac:Party/cac:PartyName/cbc:Name"/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each select="//cac:BuyerCustomerParty">
                    <tr>
                      <td style="width: 100%">
                        <b>Asıl Alıcı VKN/TCKN:</b>
                        <xsl:value-of select="cac:Party/cac:PartyIdentification/cbc:ID"/>
                      </td>
                    </tr>

                    <tr>
                      <td style="width: 100%">
                        <b>Asıl Alıcı Ünvan:</b>
                        <xsl:value-of select="cac:Party/cac:PartyName/cbc:Name"/>
                      </td>
                    </tr>
                  </xsl:for-each>
                  <xsl:for-each select="n1:DespatchAdvice/cac:Shipment/cac:Delivery/cac:DeliveryAddress">
                    <tr>
                      <td style="width: 100%">
                        <b>Teslimat Adresi:</b>
                        <br/>
                        <xsl:for-each select="cbc:StreetName">
                          <xsl:apply-templates/>
                          <xsl:text>&#160;</xsl:text>
                        </xsl:for-each>
                        <xsl:for-each select="cbc:BuildingName">
                          <xsl:apply-templates/>
                          <xsl:text>&#160;</xsl:text>
                        </xsl:for-each>
                        <xsl:for-each select="cbc:BuildingNumber">
                          <xsl:apply-templates/>
                          <xsl:text>&#160;</xsl:text>
                        </xsl:for-each>
                        <xsl:for-each select="cbc:PostalZone">
                          <xsl:apply-templates/>
                          <xsl:text>&#160;</xsl:text>
                        </xsl:for-each>
                        <xsl:for-each select="cbc:CitySubdivisionName">
                          <xsl:apply-templates/>
                          <xsl:text>/ </xsl:text>
                        </xsl:for-each>
                        <xsl:for-each select="cbc:CityName">
                          <xsl:apply-templates/>
                          <xsl:text>&#160;</xsl:text>
                        </xsl:for-each><br/>
                        <xsl:for-each select="cac:Country/cbc:Name">
                          <xsl:apply-templates/>
                          <xsl:text>&#160;</xsl:text>
                        </xsl:for-each>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each select="//cac:OriginatorCustomerParty">
                    <tr>
                      <td style="width: 100%">
                        <b>İşlemleri Başlatan Alıcı VKN:</b>
                        <xsl:value-of select="cac:Party/cac:PartyIdentification/cbc:ID"/>
                      </td>
                    </tr>

                    <tr>
                      <td style="width: 100%">
                        <b>İşlemleri Başlatan Alıcı Ünvan:</b>
                        <xsl:value-of select="cac:Party/cac:PartyName/cbc:Name"/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each select="//cac:DespatchSupplierParty/cac:DespatchContact/cbc:Name">
                    <tr>
                      <td style="width: 100%">
                        <b>Teslim Eden:</b>
                        <xsl:apply-templates/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each select="//cac:DeliveryCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID = 'BAYINO']">
                    <tr>
                      <td style="width: 100%">
                        <b>Bayi No: </b>
                        <xsl:value-of select="."/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each select="//cac:DeliveryCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID = 'SUBENO']">
                    <tr>
                      <td style="width: 100%">
                        <b>Şube No: </b>
                        <xsl:value-of select="."/>
                      </td>
                    </tr>
                  </xsl:for-each>
                </table>
              </td>

              <td style="width: 50%">
                <table style="width: 100%;" cellspacing="0" cellpadding="0">
                  <tr>
                    <td style="width: 100%">
                      <span style="font-weight:bold;">
                        Taşıyıcı Bilgileri
                      </span>
                    </td>
                  </tr>

                  <xsl:for-each select="//cac:CarrierParty">
                    <tr>
                      <td style="width: 100%">
                        <b>Taşıyıcı Firma:</b>
                        VKN: <xsl:value-of select="./cac:PartyIdentification/cbc:ID"/>,
                        <xsl:value-of select="./cac:PartyName/cbc:Name"/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each
                          select="//cac:ShipmentStage/cac:TransportMeans/cac:RoadTransport/cbc:LicensePlateID">
                    <tr>
                      <td style="width: 100%">
                        <b>Araç plaka numarası:</b>
                        <xsl:value-of select="."/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each
                          select="//cac:TransportHandlingUnit/cac:TransportEquipment/cbc:ID[@schemeID = 'DORSEPLAKA']">
                    <tr>
                      <td style="width: 100%">
                        <b>Dorse plaka numarası:</b>
                        <xsl:value-of select="."/>
                      </td>
                    </tr>
                  </xsl:for-each>

                  <xsl:for-each select="//cac:ShipmentStage/cac:DriverPerson">
                    <tr>
                      <td style="width: 100%">
                        <xsl:if test="cbc:FirstName">
                          <b>Şoför:</b>
                          <xsl:for-each select="cbc:Title">
                            <xsl:apply-templates/>
                            <xsl:text>&#160;</xsl:text>
                          </xsl:for-each>
                          <xsl:for-each select="cbc:FirstName">
                            <xsl:apply-templates/>
                            <xsl:text>&#160;</xsl:text>
                          </xsl:for-each>
                          <xsl:for-each select="cbc:MiddleName">
                            <xsl:apply-templates/>
                            <xsl:text>&#160;</xsl:text>
                          </xsl:for-each>
                          <xsl:for-each select="cbc:FamilyName">
                            <xsl:apply-templates/>
                            <xsl:text>&#160;</xsl:text>
                          </xsl:for-each>
                          , TCKN:
                          <xsl:for-each select="cbc:NationalityID">
                            <xsl:apply-templates/>
                          </xsl:for-each>
                          <br/>
                        </xsl:if>
                      </td>
                    </tr>
                  </xsl:for-each>

                </table>
              </td>

              <td style="width: 20%">

                <div id="qrcode" style="display: flex; flex-direction: column; align-items: center;"/>                <div id="qrvalue" style="visibility: hidden; height: 20px;width: 20px; ; display:none">{"vkntckn":"<xsl:value-of select="n1:DespatchAdvice/cac:DespatchSupplierParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='TCKN' or @schemeID='VKN']"/>","avkntckn":"<xsl:value-of select="n1:DespatchAdvice/cac:DeliveryCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='TCKN' or @schemeID='VKN']"/>","senaryo":"<xsl:value-of select="n1:DespatchAdvice/cbc:ProfileID"/>","tip":"<xsl:value-of select="n1:DespatchAdvice/cbc:DespatchAdviceTypeCode"/>","tarih":"<xsl:value-of select="n1:DespatchAdvice/cbc:IssueDate"/>","no":"<xsl:value-of select="n1:DespatchAdvice/cbc:ID"/>","ettn":"<xsl:value-of select="n1:DespatchAdvice/cbc:UUID"/>","sevktarihi":"<xsl:value-of select="n1:DespatchAdvice/cac:Shipment/cac:Delivery/cac:Despatch/cbc:ActualDespatchDate"/>","sevkzamani":"<xsl:value-of select="n1:DespatchAdvice/cac:Shipment/cac:Delivery/cac:Despatch/cbc:ActualDespatchTime"/>","tasiyicivkn":"<xsl:value-of select="n1:DespatchAdvice/cac:Shipment/cac:Delivery/cac:CarrierParty/cac:PartyIdentification/cbc:ID[@schemeID='VKN']"/>","plaka":"<xsl:value-of select="n1:DespatchAdvice/cac:Shipment/cac:ShipmentStage/cac:TransportMeans/cac:RoadTransport/cbc:LicensePlateID[@schemeID='PLAKA']"/>"}                </div>    
                
                <script type="text/javascript">var qrcode = new
                    QRCode(document.getElementById("qrcode"), { width :120, height :120 }); function
                  makeCode (msg) { var elText = document.getElementById("text"); qrcode.makeCode(msg);
                  } makeCode(document.getElementById("qrvalue").innerHTML);
                </script>
              </td>
            </tr>
          </tbody>
        </table>
        <!-- IBAN_REF -->

      </body>
    </html>
  </xsl:template>
  <xsl:template match="//n1:DespatchAdvice/cac:DespatchLine">
    <tr class="lineTableTr">
      <td class="lineTableTd">
        <xsl:text>&#160;</xsl:text>
        <xsl:value-of select="./cbc:ID"/>
      </td>
      <td class="lineTableTd">
        <xsl:text>&#160;</xsl:text>
        <xsl:value-of select="./cac:Item/cbc:Name"/>
      </td>

      <xsl:choose>
        <xsl:when test="$varStockCode &gt; 0">
          <td class="lineTableTd">
            <xsl:text>&#160;</xsl:text>
            <xsl:value-of select="./cac:Item/cac:SellersItemIdentification/cbc:ID"/>
          </td>
        </xsl:when>
        <xsl:otherwise>
        </xsl:otherwise>
      </xsl:choose>

      <!--      <xsl:variable name="varManufacturerItemId" select="count(//cac:ManufacturersItemIdentification[cbc:ID !=''])"/>-->

      <xsl:choose>
        <xsl:when test="$varManufacturerItemId &gt; 0">
          <td class="lineTableTd">
            <xsl:text>&#160;</xsl:text>
            <xsl:value-of select="./cac:Item/cac:ManufacturersItemIdentification/cbc:ID"/>
          </td>
        </xsl:when>
        <xsl:otherwise>
        </xsl:otherwise>
      </xsl:choose>

      <!--      <xsl:variable name="varBuyersItemId" select="count(//cac:BuyersItemIdentification[cbc:ID !=''])"/>-->

      <xsl:choose>
        <xsl:when test="$varBuyersItemId &gt; 0">
          <td class="lineTableTd">
            <xsl:text>&#160;</xsl:text>
            <xsl:value-of select="./cac:Item/cac:BuyersItemIdentification/cbc:ID"/>
          </td>
        </xsl:when>
        <xsl:otherwise>
        </xsl:otherwise>
      </xsl:choose>

      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
        <xsl:choose>
          <xsl:when test="$varInnerItemCount &gt; 0">
            <xsl:value-of select="format-number(./cbc:DeliveredQuantity, '###.###,####', 'european')"/>
            <xsl:text>(*</xsl:text>
            <xsl:value-of select="./cac:Shipment/cac:GoodsItem/cbc:Description"/>
            <xsl:text>)</xsl:text>
          </xsl:when>
          <xsl:otherwise>
            <xsl:value-of select="format-number(./cbc:DeliveredQuantity, '###.###,####', 'european')"/>
          </xsl:otherwise>
        </xsl:choose>

        <xsl:if test="./cbc:DeliveredQuantity/@unitCode">
          <xsl:for-each select="./cbc:DeliveredQuantity">
            <xsl:text> </xsl:text>
            <xsl:choose>
              <xsl:when test="@unitCode  = '26'">
                <xsl:text>ton</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'BX'">
                <xsl:text>Kutu</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'LTR'">
                <xsl:text>lt</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'C62'">
                <xsl:text>Adet</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'NIU'">
                <xsl:text>Adet</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KGM'">
                <xsl:text>kg</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KJO'">
                <xsl:text>kJ</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'GRM'">
                <xsl:text>g</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MGM'">
                <xsl:text>mg</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'NT'">
                <xsl:text>Net Ton</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'GT'">
                <xsl:text>Gross Ton</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MTR'">
                <xsl:text>m</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MMT'">
                <xsl:text>mm</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KTM'">
                <xsl:text>km</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MLT'">
                <xsl:text>ml</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MMQ'">
                <xsl:text>mm3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CLT'">
                <xsl:text>cl</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CMK'">
                <xsl:text>cm2</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CMQ'">
                <xsl:text>cm3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CMT'">
                <xsl:text>cm</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MTK'">
                <xsl:text>m2</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MTQ'">
                <xsl:text>m3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'DAY'">
                <xsl:text> Gün</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MON'">
                <xsl:text> Ay</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'PA'">
                <xsl:text> Paket</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KWH'">
                <xsl:text> KWH</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'ANN'">
                <xsl:text> Yıl</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'HUR'">
                <xsl:text> Saat</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D61'">
                <xsl:text> Dakika</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D62'">
                <xsl:text> Saniye</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CCT'">
                <xsl:text> Ton baş.taşıma kap.</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D30'">
                <xsl:text> Brüt kalori</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D40'">
                <xsl:text> 1000 lt</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'LPA'">
                <xsl:text> saf alkol lt</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'B32'">
                <xsl:text> kg.m2</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'NCL'">
                <xsl:text> hücre adet</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'PR'">
                <xsl:text> Çift</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'R9'">
                <xsl:text> 1000 m3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'SET'">
                <xsl:text> Set</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'T3'">
                <xsl:text> 1000 adet</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'PK'">
                <xsl:text> Koli</xsl:text>
              </xsl:when>
            </xsl:choose>
          </xsl:for-each>
        </xsl:if>
      </td>
      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
        <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount != 0">
          <xsl:value-of
                  select="format-number(./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount, '###.##0,########', 'european')"/>
          <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID">
            <xsl:text> </xsl:text>
            <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRL&quot; or ./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRY&quot;">
              <xsl:text>TL</xsl:text>
            </xsl:if>
            <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRL&quot; and ./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRY&quot;">
              <xsl:value-of
                      select="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID"/>
            </xsl:if>
          </xsl:if>
        </xsl:if>
      </td>
      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
        <xsl:value-of
                select="format-number(./cbc:OutstandingQuantity, '###.###,####', 'european')"/>
        <xsl:if test="./cbc:OutstandingQuantity/@unitCode">
          <xsl:for-each select="./cbc:OutstandingQuantity">
            <xsl:text> </xsl:text>
            <xsl:choose>
              <xsl:when test="@unitCode  = '26'">
                <xsl:text>ton</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'BX'">
                <xsl:text>Kutu</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'LTR'">
                <xsl:text>lt</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'C62'">
                <xsl:text>Adet</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'NIU'">
                <xsl:text>Adet</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KGM'">
                <xsl:text>kg</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KJO'">
                <xsl:text>kJ</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'GRM'">
                <xsl:text>g</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MGM'">
                <xsl:text>mg</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'NT'">
                <xsl:text>Net Ton</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'GT'">
                <xsl:text>Gross Ton</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MTR'">
                <xsl:text>m</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MMT'">
                <xsl:text>mm</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KTM'">
                <xsl:text>km</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MLT'">
                <xsl:text>ml</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MMQ'">
                <xsl:text>mm3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CLT'">
                <xsl:text>cl</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CMK'">
                <xsl:text>cm2</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CMQ'">
                <xsl:text>cm3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CMT'">
                <xsl:text>cm</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MTK'">
                <xsl:text>m2</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MTQ'">
                <xsl:text>m3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'DAY'">
                <xsl:text> Gün</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'MON'">
                <xsl:text> Ay</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'PA'">
                <xsl:text> Paket</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'KWH'">
                <xsl:text> KWH</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'ANN'">
                <xsl:text> Yıl</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'HUR'">
                <xsl:text> Saat</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D61'">
                <xsl:text> Dakika</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D62'">
                <xsl:text> Saniye</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'CCT'">
                <xsl:text> Ton baş.taşıma kap.</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D30'">
                <xsl:text> Brüt kalori</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'D40'">
                <xsl:text> 1000 lt</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'LPA'">
                <xsl:text> saf alkol lt</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'B32'">
                <xsl:text> kg.m2</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'NCL'">
                <xsl:text> hücre adet</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'PR'">
                <xsl:text> Çift</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'R9'">
                <xsl:text> 1000 m3</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'SET'">
                <xsl:text> Set</xsl:text>
              </xsl:when>
              <xsl:when test="@unitCode  = 'T3'">
                <xsl:text> 1000 adet</xsl:text>
              </xsl:when>
                <xsl:when test="@unitCode  = 'TNE'">
                    <xsl:text> ton</xsl:text>
                </xsl:when>
            </xsl:choose>
          </xsl:for-each>
        </xsl:if>
      </td>
      <xsl:choose>
        <xsl:when test="$varLineDescription &gt; 0">
          <td class="lineTableTd">
            <xsl:text>&#160;</xsl:text>
            <xsl:value-of select="./cbc:Note"/>
          </td>
        </xsl:when>
        <xsl:otherwise>
        </xsl:otherwise>
      </xsl:choose>
        <xsl:choose>
            <xsl:when test="$varKunyeNo">
                <td class="lineTableTd">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:value-of select="./cac:Item/cac:AdditionalItemIdentification/cbc:ID[@schemeID='KUNYENO']"/>
                </td>
            </xsl:when>
            <xsl:otherwise>
            </xsl:otherwise>
        </xsl:choose>
      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
        <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cac:Price/cbc:PriceAmount != 0">
          <xsl:value-of
                  select="format-number(./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount, '###.##0,########', 'european')"/>
          <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount/@currencyID">
            <xsl:text> </xsl:text>
            <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount/@currencyID = &quot;TRL&quot; or ./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount/@currencyID = &quot;TRY&quot;">
              <xsl:text>TL</xsl:text>
            </xsl:if>
            <xsl:if test="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount/@currencyID != &quot;TRL&quot; and ./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount/@currencyID != &quot;TRY&quot;">
              <xsl:value-of
                      select="./cac:Shipment/cac:GoodsItem/cac:InvoiceLine/cbc:LineExtensionAmount/@currencyID"/>
            </xsl:if>
          </xsl:if>
        </xsl:if>
      </td>
    </tr>
  </xsl:template>
  <xsl:template match="//cbc:IssueDate">
    <xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of
          select="substring(.,1,4)"/>
  </xsl:template>
  <xsl:template match="//cbc:ActualDespatchDate">
    <xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of
          select="substring(.,1,4)"/>
  </xsl:template>
  <xsl:template match="//n1:DespatchAdvice">
    <tr class="lineTableTr">
      <td class="lineTableTd">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td class="lineTableTd">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
      <td class="lineTableTd" align="right">
        <xsl:text>&#160;</xsl:text>
      </td>
    </tr>
  </xsl:template>
  <xsl:template name="Party_Title">
    <xsl:param name="PartyType"/>
    <td style="width:469px; " align="left">
      <xsl:if test="cac:PartyName">
        <xsl:value-of select="cac:PartyName/cbc:Name"/>
        <br/>
      </xsl:if>
      <xsl:for-each select="cac:Person">
        <xsl:for-each select="cbc:Title">
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <xsl:for-each select="cbc:FirstName">
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <xsl:for-each select="cbc:MiddleName">
          <xsl:apply-templates/>
          <xsl:text>&#160; </xsl:text>
        </xsl:for-each>
        <xsl:for-each select="cbc:FamilyName">
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <xsl:for-each select="cbc:NameSuffix">
          <xsl:apply-templates/>
        </xsl:for-each>
        <xsl:if test="$PartyType='TAXFREE'">
          <br/>
          <xsl:text>Pasaport No: </xsl:text>
          <xsl:value-of select="cac:IdentityDocumentReference/cbc:ID"/>
          <br/>
          <xsl:text>Ülkesi: </xsl:text>
          <xsl:value-of select="cbc:NationalityID"/>
        </xsl:if>
      </xsl:for-each>
    </td>
  </xsl:template>
  <xsl:template name="Party_Adress">
    <xsl:param name="PartyType"/>
    <td style="width:469px; " align="left">
      <xsl:for-each select="cac:PostalAddress">
        <xsl:for-each select="cbc:StreetName">
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <xsl:for-each select="cbc:BuildingName">
          <xsl:apply-templates/>
        </xsl:for-each>
        <xsl:for-each select="cbc:BuildingNumber">
          <xsl:text> No:</xsl:text>
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <br/>
        <xsl:for-each select="cbc:Room">
          <xsl:text>Kapı No:</xsl:text>
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <br/>
        <xsl:for-each select="cbc:PostalZone">
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <xsl:for-each select="cbc:CitySubdivisionName">
          <xsl:apply-templates/>
          <xsl:text>/ </xsl:text>
        </xsl:for-each>
        <xsl:for-each select="cbc:CityName">
          <xsl:apply-templates/>
          <xsl:text>&#160;</xsl:text>
        </xsl:for-each>
        <xsl:if test="$PartyType='TAXFREE'">
          <br/>
          <xsl:value-of select="cac:Country/cbc:Name"/>
          <br/>
        </xsl:if>
      </xsl:for-each>
    </td>
  </xsl:template>
  <xsl:template name='Party_Other'>
    <xsl:param name="PartyType"/>
    <xsl:for-each select="cbc:WebsiteURI">
      <tr align="left">
        <td>
          <xsl:text>Web Sitesi: </xsl:text>
          <xsl:value-of select="."/>
        </td>
      </tr>
    </xsl:for-each>
    <xsl:for-each select="cac:Contact/cbc:ElectronicMail">
      <tr align="left">
        <td>
          <xsl:text>E-Posta: </xsl:text>
          <xsl:value-of select="."/>
        </td>
      </tr>
    </xsl:for-each>
    <xsl:for-each select="cac:Contact">
      <xsl:if test="cbc:Telephone or cbc:Telefax">
        <tr align="left">
          <td style="width:469px; " align="left">
            <xsl:for-each select="cbc:Telephone">
              <xsl:text>Tel: </xsl:text>
              <xsl:apply-templates/>
            </xsl:for-each>
            <xsl:for-each select="cbc:Telefax">
              <xsl:text> Fax: </xsl:text>
              <xsl:apply-templates/>
            </xsl:for-each>
            <xsl:text>&#160;</xsl:text>
          </td>
        </tr>
      </xsl:if>
    </xsl:for-each>
    <xsl:if test="$PartyType!='TAXFREE'">
      <xsl:for-each select="cac:PartyTaxScheme/cac:TaxScheme/cbc:Name">
        <tr align="left">
          <td>
            <xsl:text>Vergi Dairesi: </xsl:text>
            <xsl:apply-templates/>
          </td>
        </tr>
      </xsl:for-each>
      <xsl:for-each select="cac:PartyIdentification">
        <tr align="left">
          <td>
            <xsl:value-of select="cbc:ID/@schemeID"/>
            <xsl:text>: </xsl:text>
            <xsl:value-of select="cbc:ID"/>
          </td>
        </tr>
      </xsl:for-each>
    </xsl:if>
  </xsl:template>
  <xsl:template name="Curr_Type">
    <xsl:value-of select="format-number(., '###.##0,00', 'european')"/>
    <xsl:if test="@currencyID">
      <xsl:text> </xsl:text>
      <xsl:choose>
        <xsl:when test="@currencyID = 'TRL' or @currencyID = 'TRY'">
          <xsl:text>TL</xsl:text>
        </xsl:when>
        <xsl:otherwise>
          <xsl:value-of select="@currencyID"/>
        </xsl:otherwise>
      </xsl:choose>
    </xsl:if>
  </xsl:template>
</xsl:stylesheet>