<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
                xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
                xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2"
                xmlns:ccts="urn:un:unece:uncefact:documentation:2"
                xmlns:clm54217="urn:un:unece:uncefact:codelist:specification:54217:2001"
                xmlns:clm5639="urn:un:unece:uncefact:codelist:specification:5639:1988"
                xmlns:clm66411="urn:un:unece:uncefact:codelist:specification:66411:2001"
                xmlns:clmIANAMIMEMediaType="urn:un:unece:uncefact:codelist:specification:IANAMIMEMediaType:2003"
                xmlns:fn="http://www.w3.org/2005/xpath-functions" xmlns:link="http://www.xbrl.org/2003/linkbase"
                xmlns:n1="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
                xmlns:qdt="urn:oasis:names:specification:ubl:schema:xsd:QualifiedDatatypes-2"
                xmlns:udt="urn:un:unece:uncefact:data:specification:UnqualifiedDataTypesSchemaModule:2"
                xmlns:xbrldi="http://xbrl.org/2006/xbrldi" xmlns:xbrli="http://www.xbrl.org/2003/instance"
                xmlns:xdt="http://www.w3.org/2005/xpath-datatypes" xmlns:xlink="http://www.w3.org/1999/xlink"
                xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:xsd="http://www.w3.org/2001/XMLSchema"
                xmlns:lcl="http://www.efatura.gov.tr/local"
                xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                exclude-result-prefixes="cac cbc ccts clm54217 clm5639 clm66411 clmIANAMIMEMediaType fn link n1 qdt udt xbrldi xbrli xdt xlink xs xsd xsi lcl">
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
    <xsl:variable name="vareczanehizmetbedeli">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_EHB:'">
                    <xsl:variable name="vareczanehizmetbedeliControl" select="normalize-space(substring-after(substring(.,8),':'))"/>
                    <xsl:choose>
                        <xsl:when test="$vareczanehizmetbedeliControl != 'null' and $vareczanehizmetbedeliControl">
                            <xsl:value-of select="$vareczanehizmetbedeliControl"/>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="vareczanehizmetbedeli20">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,11) = 'SGK_EHB20:'">
                    <xsl:variable name="vareczanehizmetbedeli20Control" select="normalize-space(substring-after(substring(.,8),':'))"/>
                    <xsl:choose>
                        <xsl:when test="$vareczanehizmetbedeli20Control != 'null' and $vareczanehizmetbedeli20Control">
                            <xsl:value-of select="$vareczanehizmetbedeli20Control"/>
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="vartutar">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_BRT:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="variskonto">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_ISK:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varkatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_HKP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varisitmekatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_IKP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varilacfarki">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_ILF:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varsiparissorumlusu">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,4) = 'SS:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,3),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varkdv8">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_K08:'">
                    <xsl:variable name="varkdv8Control" select="normalize-space(substring-after(substring(.,8),':'))" />
                    <xsl:choose>
                        <xsl:when test="$varkdv8Control != 'null' and $varkdv8Control">
                            <xsl:value-of select="$varkdv8Control" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varkdv10">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_K10:'">
                    <xsl:variable name="varkdv10Control" select="normalize-space(substring-after(substring(.,8),':'))" />
                    <xsl:choose>
                        <xsl:when test="$varkdv10Control != 'null' and $varkdv10Control != 'undefined'">
                            <xsl:value-of select="$varkdv10Control" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varkdv18">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_K18:'">
                    <xsl:variable name="varkdv18Control" select="normalize-space(substring-after(substring(.,8),':'))" />
                    <xsl:choose>
                        <xsl:when test="$varkdv18Control != 'null' and $varkdv18Control != 'undefined'">
                            <xsl:value-of select="$varkdv18Control" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varkdv20">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_K20:'">
                    <xsl:variable name="varkdv20Control" select="normalize-space(substring-after(substring(.,8),':'))" />
                    <xsl:choose>
                        <xsl:when test="$varkdv20Control != 'null' and $varkdv20Control != 'undefined'">
                            <xsl:value-of select="$varkdv20Control" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="vareczanekdv18">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_EHK:'">
                    <xsl:variable name="vareczanekdv18Control" select="normalize-space(substring-after(substring(.,8),':'))" />
                    <xsl:choose>
                        <xsl:when test="$vareczanekdv18Control != 'null' and $vareczanekdv18Control != 'undefined'">
                            <xsl:value-of select="$vareczanekdv18Control" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="vareczanekdv20">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,11) = 'SGK_EHK20:'">
                    <xsl:variable name="vareczanekdv20Control" select="normalize-space(substring-after(substring(.,8),':'))" />
                    <xsl:choose>
                        <xsl:when test="$vareczanekdv20Control != 'null' and $vareczanekdv20Control != 'undefined'">
                            <xsl:value-of select="$vareczanekdv20Control" />
                        </xsl:when>
                        <xsl:otherwise>
                            <xsl:value-of select="0"/>
                        </xsl:otherwise>
                    </xsl:choose>
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varpsf">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_PSF:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varreceteadedi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_RCA:'">
                    <xsl:value-of select="substring-after(substring(.,8),':')" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="vareldenilackatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_EIP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varmaasdanilackatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_MIP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="vareldenmuayenekatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_EMP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varmaasmuayenekatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_MMP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="vareldenrecetekatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_ERP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varmaastanrecetekatilimpayi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_MRP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,8),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varfaturatipi">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,12) = 'FATURATIPI:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,10),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varoptik">
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,9) = 'SGK_TYP:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,7),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varfaturatype">
    <!--        Cetas ise kullanılacak-->
        <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
        Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
        <xsl:for-each select="//n1:Invoice/cbc:Note">
            <xsl:choose>
                <xsl:when test="substring(.,0,13) = 'FATURA_TYPE:'">
                    <xsl:value-of select="normalize-space(substring-after(substring(.,11),':'))" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varExportInsurance" select="count(//cac:Shipment[cbc:InsuranceValueAmount])"/>
    <xsl:variable name="varExportCarriage" select="count(//cac:Shipment[cbc:DeclaredForCarriageValueAmount])"/>
    <xsl:variable name="varEtiketFiyati">
        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
            <xsl:for-each select="cbc:Note">
                <xsl:choose>
                    <xsl:when test="substring(.,0,5) = 'ETF:' or substring(.,0,5) = 'ESF:'">
                        <xsl:value-of select="1" />
                    </xsl:when>
                </xsl:choose>
            </xsl:for-each>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varEczaciKar">
        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
            <xsl:for-each select="cbc:Note">
                <xsl:choose>
                    <xsl:when test="substring(.,0,5) = 'ECK:' or substring(.,0,5) = 'EKO:'">
                        <xsl:value-of select="1" />
                    </xsl:when>
                </xsl:choose>
            </xsl:for-each>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varKurumIskonto">
        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
            <xsl:for-each select="cbc:Note">
                <xsl:choose>
                    <xsl:when test="substring(.,0,5) = 'KRI:'">
                        <xsl:value-of select="1" />
                    </xsl:when>
                </xsl:choose>
            </xsl:for-each>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varDepocuFiyati">
        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
            <xsl:for-each select="cbc:Note">
                <xsl:choose>
                    <xsl:when test="substring(.,0,5) = 'DSF:'">
                        <xsl:value-of select="1" />
                    </xsl:when>
                </xsl:choose>
            </xsl:for-each>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varVade">
        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
            <xsl:for-each select="cbc:Note">
                <xsl:choose>
                    <xsl:when test="substring(.,0,5) = 'VAD:'">
                        <xsl:value-of select="1" />
                    </xsl:when>
                </xsl:choose>
            </xsl:for-each>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varBranchName">
        <xsl:for-each select="//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentTypeCode[text()='SUBE_UNVAN']">
            <xsl:choose>
                <xsl:when   test="../cbc:DocumentTypeCode='SUBE_UNVAN'">
                    <xsl:value-of select="1" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>

    <xsl:variable name="varSenaryoName">
        <xsl:for-each select="//n1:Invoice/cbc:ProfileID">
            <xsl:choose>
                <xsl:when test="substring(.,0,5) = 'KAMU'">
                    <xsl:value-of select="1" />
                </xsl:when>
            </xsl:choose>
        </xsl:for-each>
    </xsl:variable>
    <xsl:variable name="varVknComp">
        <xsl:if test="(//n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='VKN'])=(//n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='VKN'])">
            <xsl:value-of select="1" />
        </xsl:if>
    </xsl:variable>

    <xsl:variable name="varItemCode" select="count(//cac:SellersItemIdentification[cbc:ID !=''])"/>
    <xsl:variable name="varAllowanceRate" select="count(//n1:Invoice/cac:InvoiceLine/cac:AllowanceCharge[cbc:MultiplierFactorNumeric !=''])"/>
    <xsl:variable name="varAllowanceAmount" select="count(//n1:Invoice/cac:InvoiceLine/cac:AllowanceCharge[cbc:Amount !=''])"/>
    <xsl:variable name="varAllowanceReason" select="count(//n1:Invoice/cac:InvoiceLine/cac:AllowanceCharge[cbc:AllowanceChargeReason !=''])"/>
    <xsl:variable name="varLineExplanation" select="count(//n1:Invoice/cac:InvoiceLine[cbc:Note !=''])"/>

    <xsl:template match="/">
        <html>
            <head>
                <script type="text/javascript">
                    <![CDATA[var QRCode;!function(){function a(a){this.mode=c.MODE_8BIT_BYTE,this.data=a,this.parsedData=[];for(var b=[],d=0,e=this.data.length;e>d;d++){var f=this.data.charCodeAt(d);f>65536?(b[0]=240|(1835008&f)>>>18,b[1]=128|(258048&f)>>>12,b[2]=128|(4032&f)>>>6,b[3]=128|63&f):f>2048?(b[0]=224|(61440&f)>>>12,b[1]=128|(4032&f)>>>6,b[2]=128|63&f):f>128?(b[0]=192|(1984&f)>>>6,b[1]=128|63&f):b[0]=f,this.parsedData=this.parsedData.concat(b)}this.parsedData.length!=this.data.length&&(this.parsedData.unshift(191),this.parsedData.unshift(187),this.parsedData.unshift(239))}function b(a,b){this.typeNumber=a,this.errorCorrectLevel=b,this.modules=null,this.moduleCount=0,this.dataCache=null,this.dataList=[]}function i(a,b){if(void 0==a.length)throw new Error(a.length+"/"+b);for(var c=0;c<a.length&&0==a[c];)c++;this.num=new Array(a.length-c+b);for(var d=0;d<a.length-c;d++)this.num[d]=a[d+c]}function j(a,b){this.totalCount=a,this.dataCount=b}function k(){this.buffer=[],this.length=0}function m(){return"undefined"!=typeof CanvasRenderingContext2D}function n(){var a=!1,b=navigator.userAgent;return/android/i.test(b)&&(a=!0,aMat=b.toString().match(/android ([0-9]\.[0-9])/i),aMat&&aMat[1]&&(a=parseFloat(aMat[1]))),a}function r(a,b){for(var c=1,e=s(a),f=0,g=l.length;g>=f;f++){var h=0;switch(b){case d.L:h=l[f][0];break;case d.M:h=l[f][1];break;case d.Q:h=l[f][2];break;case d.H:h=l[f][3]}if(h>=e)break;c++}if(c>l.length)throw new Error("Too long data");return c}function s(a){var b=encodeURI(a).toString().replace(/\%[0-9a-fA-F]{2}/g,"a");return b.length+(b.length!=a?3:0)}a.prototype={getLength:function(){return this.parsedData.length},write:function(a){for(var b=0,c=this.parsedData.length;c>b;b++)a.put(this.parsedData[b],8)}},b.prototype={addData:function(b){var c=new a(b);this.dataList.push(c),this.dataCache=null},isDark:function(a,b){if(0>a||this.moduleCount<=a||0>b||this.moduleCount<=b)throw new Error(a+","+b);return this.modules[a][b]},getModuleCount:function(){return this.moduleCount},make:function(){this.makeImpl(!1,this.getBestMaskPattern())},makeImpl:function(a,c){this.moduleCount=4*this.typeNumber+17,this.modules=new Array(this.moduleCount);for(var d=0;d<this.moduleCount;d++){this.modules[d]=new Array(this.moduleCount);for(var e=0;e<this.moduleCount;e++)this.modules[d][e]=null}this.setupPositionProbePattern(0,0),this.setupPositionProbePattern(this.moduleCount-7,0),this.setupPositionProbePattern(0,this.moduleCount-7),this.setupPositionAdjustPattern(),this.setupTimingPattern(),this.setupTypeInfo(a,c),this.typeNumber>=7&&this.setupTypeNumber(a),null==this.dataCache&&(this.dataCache=b.createData(this.typeNumber,this.errorCorrectLevel,this.dataList)),this.mapData(this.dataCache,c)},setupPositionProbePattern:function(a,b){for(var c=-1;7>=c;c++)if(!(-1>=a+c||this.moduleCount<=a+c))for(var d=-1;7>=d;d++)-1>=b+d||this.moduleCount<=b+d||(this.modules[a+c][b+d]=c>=0&&6>=c&&(0==d||6==d)||d>=0&&6>=d&&(0==c||6==c)||c>=2&&4>=c&&d>=2&&4>=d?!0:!1)},getBestMaskPattern:function(){for(var a=0,b=0,c=0;8>c;c++){this.makeImpl(!0,c);var d=f.getLostPoint(this);(0==c||a>d)&&(a=d,b=c)}return b},createMovieClip:function(a,b,c){var d=a.createEmptyMovieClip(b,c),e=1;this.make();for(var f=0;f<this.modules.length;f++)for(var g=f*e,h=0;h<this.modules[f].length;h++){var i=h*e,j=this.modules[f][h];j&&(d.beginFill(0,100),d.moveTo(i,g),d.lineTo(i+e,g),d.lineTo(i+e,g+e),d.lineTo(i,g+e),d.endFill())}return d},setupTimingPattern:function(){for(var a=8;a<this.moduleCount-8;a++)null==this.modules[a][6]&&(this.modules[a][6]=0==a%2);for(var b=8;b<this.moduleCount-8;b++)null==this.modules[6][b]&&(this.modules[6][b]=0==b%2)},setupPositionAdjustPattern:function(){for(var a=f.getPatternPosition(this.typeNumber),b=0;b<a.length;b++)for(var c=0;c<a.length;c++){var d=a[b],e=a[c];if(null==this.modules[d][e])for(var g=-2;2>=g;g++)for(var h=-2;2>=h;h++)this.modules[d+g][e+h]=-2==g||2==g||-2==h||2==h||0==g&&0==h?!0:!1}},setupTypeNumber:function(a){for(var b=f.getBCHTypeNumber(this.typeNumber),c=0;18>c;c++){var d=!a&&1==(1&b>>c);this.modules[Math.floor(c/3)][c%3+this.moduleCount-8-3]=d}for(var c=0;18>c;c++){var d=!a&&1==(1&b>>c);this.modules[c%3+this.moduleCount-8-3][Math.floor(c/3)]=d}},setupTypeInfo:function(a,b){for(var c=this.errorCorrectLevel<<3|b,d=f.getBCHTypeInfo(c),e=0;15>e;e++){var g=!a&&1==(1&d>>e);6>e?this.modules[e][8]=g:8>e?this.modules[e+1][8]=g:this.modules[this.moduleCount-15+e][8]=g}for(var e=0;15>e;e++){var g=!a&&1==(1&d>>e);8>e?this.modules[8][this.moduleCount-e-1]=g:9>e?this.modules[8][15-e-1+1]=g:this.modules[8][15-e-1]=g}this.modules[this.moduleCount-8][8]=!a},mapData:function(a,b){for(var c=-1,d=this.moduleCount-1,e=7,g=0,h=this.moduleCount-1;h>0;h-=2)for(6==h&&h--;;){for(var i=0;2>i;i++)if(null==this.modules[d][h-i]){var j=!1;g<a.length&&(j=1==(1&a[g]>>>e));var k=f.getMask(b,d,h-i);k&&(j=!j),this.modules[d][h-i]=j,e--,-1==e&&(g++,e=7)}if(d+=c,0>d||this.moduleCount<=d){d-=c,c=-c;break}}}},b.PAD0=236,b.PAD1=17,b.createData=function(a,c,d){for(var e=j.getRSBlocks(a,c),g=new k,h=0;h<d.length;h++){var i=d[h];g.put(i.mode,4),g.put(i.getLength(),f.getLengthInBits(i.mode,a)),i.write(g)}for(var l=0,h=0;h<e.length;h++)l+=e[h].dataCount;if(g.getLengthInBits()>8*l)throw new Error("code length overflow. ("+g.getLengthInBits()+">"+8*l+")");for(g.getLengthInBits()+4<=8*l&&g.put(0,4);0!=g.getLengthInBits()%8;)g.putBit(!1);for(;;){if(g.getLengthInBits()>=8*l)break;if(g.put(b.PAD0,8),g.getLengthInBits()>=8*l)break;g.put(b.PAD1,8)}return b.createBytes(g,e)},b.createBytes=function(a,b){for(var c=0,d=0,e=0,g=new Array(b.length),h=new Array(b.length),j=0;j<b.length;j++){var k=b[j].dataCount,l=b[j].totalCount-k;d=Math.max(d,k),e=Math.max(e,l),g[j]=new Array(k);for(var m=0;m<g[j].length;m++)g[j][m]=255&a.buffer[m+c];c+=k;var n=f.getErrorCorrectPolynomial(l),o=new i(g[j],n.getLength()-1),p=o.mod(n);h[j]=new Array(n.getLength()-1);for(var m=0;m<h[j].length;m++){var q=m+p.getLength()-h[j].length;h[j][m]=q>=0?p.get(q):0}}for(var r=0,m=0;m<b.length;m++)r+=b[m].totalCount;for(var s=new Array(r),t=0,m=0;d>m;m++)for(var j=0;j<b.length;j++)m<g[j].length&&(s[t++]=g[j][m]);for(var m=0;e>m;m++)for(var j=0;j<b.length;j++)m<h[j].length&&(s[t++]=h[j][m]);return s};for(var c={MODE_NUMBER:1,MODE_ALPHA_NUM:2,MODE_8BIT_BYTE:4,MODE_KANJI:8},d={L:1,M:0,Q:3,H:2},e={PATTERN000:0,PATTERN001:1,PATTERN010:2,PATTERN011:3,PATTERN100:4,PATTERN101:5,PATTERN110:6,PATTERN111:7},f={PATTERN_POSITION_TABLE:[[],[6,18],[6,22],[6,26],[6,30],[6,34],[6,22,38],[6,24,42],[6,26,46],[6,28,50],[6,30,54],[6,32,58],[6,34,62],[6,26,46,66],[6,26,48,70],[6,26,50,74],[6,30,54,78],[6,30,56,82],[6,30,58,86],[6,34,62,90],[6,28,50,72,94],[6,26,50,74,98],[6,30,54,78,102],[6,28,54,80,106],[6,32,58,84,110],[6,30,58,86,114],[6,34,62,90,118],[6,26,50,74,98,122],[6,30,54,78,102,126],[6,26,52,78,104,130],[6,30,56,82,108,134],[6,34,60,86,112,138],[6,30,58,86,114,142],[6,34,62,90,118,146],[6,30,54,78,102,126,150],[6,24,50,76,102,128,154],[6,28,54,80,106,132,158],[6,32,58,84,110,136,162],[6,26,54,82,110,138,166],[6,30,58,86,114,142,170]],G15:1335,G18:7973,G15_MASK:21522,getBCHTypeInfo:function(a){for(var b=a<<10;f.getBCHDigit(b)-f.getBCHDigit(f.G15)>=0;)b^=f.G15<<f.getBCHDigit(b)-f.getBCHDigit(f.G15);return(a<<10|b)^f.G15_MASK},getBCHTypeNumber:function(a){for(var b=a<<12;f.getBCHDigit(b)-f.getBCHDigit(f.G18)>=0;)b^=f.G18<<f.getBCHDigit(b)-f.getBCHDigit(f.G18);return a<<12|b},getBCHDigit:function(a){for(var b=0;0!=a;)b++,a>>>=1;return b},getPatternPosition:function(a){return f.PATTERN_POSITION_TABLE[a-1]},getMask:function(a,b,c){switch(a){case e.PATTERN000:return 0==(b+c)%2;case e.PATTERN001:return 0==b%2;case e.PATTERN010:return 0==c%3;case e.PATTERN011:return 0==(b+c)%3;case e.PATTERN100:return 0==(Math.floor(b/2)+Math.floor(c/3))%2;case e.PATTERN101:return 0==b*c%2+b*c%3;case e.PATTERN110:return 0==(b*c%2+b*c%3)%2;case e.PATTERN111:return 0==(b*c%3+(b+c)%2)%2;default:throw new Error("bad maskPattern:"+a)}},getErrorCorrectPolynomial:function(a){for(var b=new i([1],0),c=0;a>c;c++)b=b.multiply(new i([1,g.gexp(c)],0));return b},getLengthInBits:function(a,b){if(b>=1&&10>b)switch(a){case c.MODE_NUMBER:return 10;case c.MODE_ALPHA_NUM:return 9;case c.MODE_8BIT_BYTE:return 8;case c.MODE_KANJI:return 8;default:throw new Error("mode:"+a)}else if(27>b)switch(a){case c.MODE_NUMBER:return 12;case c.MODE_ALPHA_NUM:return 11;case c.MODE_8BIT_BYTE:return 16;case c.MODE_KANJI:return 10;default:throw new Error("mode:"+a)}else{if(!(41>b))throw new Error("type:"+b);switch(a){case c.MODE_NUMBER:return 14;case c.MODE_ALPHA_NUM:return 13;case c.MODE_8BIT_BYTE:return 16;case c.MODE_KANJI:return 12;default:throw new Error("mode:"+a)}}},getLostPoint:function(a){for(var b=a.getModuleCount(),c=0,d=0;b>d;d++)for(var e=0;b>e;e++){for(var f=0,g=a.isDark(d,e),h=-1;1>=h;h++)if(!(0>d+h||d+h>=b))for(var i=-1;1>=i;i++)0>e+i||e+i>=b||(0!=h||0!=i)&&g==a.isDark(d+h,e+i)&&f++;f>5&&(c+=3+f-5)}for(var d=0;b-1>d;d++)for(var e=0;b-1>e;e++){var j=0;a.isDark(d,e)&&j++,a.isDark(d+1,e)&&j++,a.isDark(d,e+1)&&j++,a.isDark(d+1,e+1)&&j++,(0==j||4==j)&&(c+=3)}for(var d=0;b>d;d++)for(var e=0;b-6>e;e++)a.isDark(d,e)&&!a.isDark(d,e+1)&&a.isDark(d,e+2)&&a.isDark(d,e+3)&&a.isDark(d,e+4)&&!a.isDark(d,e+5)&&a.isDark(d,e+6)&&(c+=40);for(var e=0;b>e;e++)for(var d=0;b-6>d;d++)a.isDark(d,e)&&!a.isDark(d+1,e)&&a.isDark(d+2,e)&&a.isDark(d+3,e)&&a.isDark(d+4,e)&&!a.isDark(d+5,e)&&a.isDark(d+6,e)&&(c+=40);for(var k=0,e=0;b>e;e++)for(var d=0;b>d;d++)a.isDark(d,e)&&k++;var l=Math.abs(100*k/b/b-50)/5;return c+=10*l}},g={glog:function(a){if(1>a)throw new Error("glog("+a+")");return g.LOG_TABLE[a]},gexp:function(a){for(;0>a;)a+=255;for(;a>=256;)a-=255;return g.EXP_TABLE[a]},EXP_TABLE:new Array(256),LOG_TABLE:new Array(256)},h=0;8>h;h++)g.EXP_TABLE[h]=1<<h;for(var h=8;256>h;h++)g.EXP_TABLE[h]=g.EXP_TABLE[h-4]^g.EXP_TABLE[h-5]^g.EXP_TABLE[h-6]^g.EXP_TABLE[h-8];for(var h=0;255>h;h++)g.LOG_TABLE[g.EXP_TABLE[h]]=h;i.prototype={get:function(a){return this.num[a]},getLength:function(){return this.num.length},multiply:function(a){for(var b=new Array(this.getLength()+a.getLength()-1),c=0;c<this.getLength();c++)for(var d=0;d<a.getLength();d++)b[c+d]^=g.gexp(g.glog(this.get(c))+g.glog(a.get(d)));return new i(b,0)},mod:function(a){if(this.getLength()-a.getLength()<0)return this;for(var b=g.glog(this.get(0))-g.glog(a.get(0)),c=new Array(this.getLength()),d=0;d<this.getLength();d++)c[d]=this.get(d);for(var d=0;d<a.getLength();d++)c[d]^=g.gexp(g.glog(a.get(d))+b);return new i(c,0).mod(a)}},j.RS_BLOCK_TABLE=[[1,26,19],[1,26,16],[1,26,13],[1,26,9],[1,44,34],[1,44,28],[1,44,22],[1,44,16],[1,70,55],[1,70,44],[2,35,17],[2,35,13],[1,100,80],[2,50,32],[2,50,24],[4,25,9],[1,134,108],[2,67,43],[2,33,15,2,34,16],[2,33,11,2,34,12],[2,86,68],[4,43,27],[4,43,19],[4,43,15],[2,98,78],[4,49,31],[2,32,14,4,33,15],[4,39,13,1,40,14],[2,121,97],[2,60,38,2,61,39],[4,40,18,2,41,19],[4,40,14,2,41,15],[2,146,116],[3,58,36,2,59,37],[4,36,16,4,37,17],[4,36,12,4,37,13],[2,86,68,2,87,69],[4,69,43,1,70,44],[6,43,19,2,44,20],[6,43,15,2,44,16],[4,101,81],[1,80,50,4,81,51],[4,50,22,4,51,23],[3,36,12,8,37,13],[2,116,92,2,117,93],[6,58,36,2,59,37],[4,46,20,6,47,21],[7,42,14,4,43,15],[4,133,107],[8,59,37,1,60,38],[8,44,20,4,45,21],[12,33,11,4,34,12],[3,145,115,1,146,116],[4,64,40,5,65,41],[11,36,16,5,37,17],[11,36,12,5,37,13],[5,109,87,1,110,88],[5,65,41,5,66,42],[5,54,24,7,55,25],[11,36,12],[5,122,98,1,123,99],[7,73,45,3,74,46],[15,43,19,2,44,20],[3,45,15,13,46,16],[1,135,107,5,136,108],[10,74,46,1,75,47],[1,50,22,15,51,23],[2,42,14,17,43,15],[5,150,120,1,151,121],[9,69,43,4,70,44],[17,50,22,1,51,23],[2,42,14,19,43,15],[3,141,113,4,142,114],[3,70,44,11,71,45],[17,47,21,4,48,22],[9,39,13,16,40,14],[3,135,107,5,136,108],[3,67,41,13,68,42],[15,54,24,5,55,25],[15,43,15,10,44,16],[4,144,116,4,145,117],[17,68,42],[17,50,22,6,51,23],[19,46,16,6,47,17],[2,139,111,7,140,112],[17,74,46],[7,54,24,16,55,25],[34,37,13],[4,151,121,5,152,122],[4,75,47,14,76,48],[11,54,24,14,55,25],[16,45,15,14,46,16],[6,147,117,4,148,118],[6,73,45,14,74,46],[11,54,24,16,55,25],[30,46,16,2,47,17],[8,132,106,4,133,107],[8,75,47,13,76,48],[7,54,24,22,55,25],[22,45,15,13,46,16],[10,142,114,2,143,115],[19,74,46,4,75,47],[28,50,22,6,51,23],[33,46,16,4,47,17],[8,152,122,4,153,123],[22,73,45,3,74,46],[8,53,23,26,54,24],[12,45,15,28,46,16],[3,147,117,10,148,118],[3,73,45,23,74,46],[4,54,24,31,55,25],[11,45,15,31,46,16],[7,146,116,7,147,117],[21,73,45,7,74,46],[1,53,23,37,54,24],[19,45,15,26,46,16],[5,145,115,10,146,116],[19,75,47,10,76,48],[15,54,24,25,55,25],[23,45,15,25,46,16],[13,145,115,3,146,116],[2,74,46,29,75,47],[42,54,24,1,55,25],[23,45,15,28,46,16],[17,145,115],[10,74,46,23,75,47],[10,54,24,35,55,25],[19,45,15,35,46,16],[17,145,115,1,146,116],[14,74,46,21,75,47],[29,54,24,19,55,25],[11,45,15,46,46,16],[13,145,115,6,146,116],[14,74,46,23,75,47],[44,54,24,7,55,25],[59,46,16,1,47,17],[12,151,121,7,152,122],[12,75,47,26,76,48],[39,54,24,14,55,25],[22,45,15,41,46,16],[6,151,121,14,152,122],[6,75,47,34,76,48],[46,54,24,10,55,25],[2,45,15,64,46,16],[17,152,122,4,153,123],[29,74,46,14,75,47],[49,54,24,10,55,25],[24,45,15,46,46,16],[4,152,122,18,153,123],[13,74,46,32,75,47],[48,54,24,14,55,25],[42,45,15,32,46,16],[20,147,117,4,148,118],[40,75,47,7,76,48],[43,54,24,22,55,25],[10,45,15,67,46,16],[19,148,118,6,149,119],[18,75,47,31,76,48],[34,54,24,34,55,25],[20,45,15,61,46,16]],j.getRSBlocks=function(a,b){var c=j.getRsBlockTable(a,b);if(void 0==c)throw new Error("bad rs block @ typeNumber:"+a+"/errorCorrectLevel:"+b);for(var d=c.length/3,e=[],f=0;d>f;f++)for(var g=c[3*f+0],h=c[3*f+1],i=c[3*f+2],k=0;g>k;k++)e.push(new j(h,i));return e},j.getRsBlockTable=function(a,b){switch(b){case d.L:return j.RS_BLOCK_TABLE[4*(a-1)+0];case d.M:return j.RS_BLOCK_TABLE[4*(a-1)+1];case d.Q:return j.RS_BLOCK_TABLE[4*(a-1)+2];case d.H:return j.RS_BLOCK_TABLE[4*(a-1)+3];default:return void 0}},k.prototype={get:function(a){var b=Math.floor(a/8);return 1==(1&this.buffer[b]>>>7-a%8)},put:function(a,b){for(var c=0;b>c;c++)this.putBit(1==(1&a>>>b-c-1))},getLengthInBits:function(){return this.length},putBit:function(a){var b=Math.floor(this.length/8);this.buffer.length<=b&&this.buffer.push(0),a&&(this.buffer[b]|=128>>>this.length%8),this.length++}};var l=[[17,14,11,7],[32,26,20,14],[53,42,32,24],[78,62,46,34],[106,84,60,44],[134,106,74,58],[154,122,86,64],[192,152,108,84],[230,180,130,98],[271,213,151,119],[321,251,177,137],[367,287,203,155],[425,331,241,177],[458,362,258,194],[520,412,292,220],[586,450,322,250],[644,504,364,280],[718,560,394,310],[792,624,442,338],[858,666,482,382],[929,711,509,403],[1003,779,565,439],[1091,857,611,461],[1171,911,661,511],[1273,997,715,535],[1367,1059,751,593],[1465,1125,805,625],[1528,1190,868,658],[1628,1264,908,698],[1732,1370,982,742],[1840,1452,1030,790],[1952,1538,1112,842],[2068,1628,1168,898],[2188,1722,1228,958],[2303,1809,1283,983],[2431,1911,1351,1051],[2563,1989,1423,1093],[2699,2099,1499,1139],[2809,2213,1579,1219],[2953,2331,1663,1273]],o=function(){var a=function(a,b){this._el=a,this._htOption=b};return a.prototype.draw=function(a){function g(a,b){var c=document.createElementNS("http://www.w3.org/2000/svg",a);for(var d in b)b.hasOwnProperty(d)&&c.setAttribute(d,b[d]);return c}var b=this._htOption,c=this._el,d=a.getModuleCount();Math.floor(b.width/d),Math.floor(b.height/d),this.clear();var h=g("svg",{viewBox:"0 0 "+String(d)+" "+String(d),width:"100%",height:"100%",fill:b.colorLight});h.setAttributeNS("http://www.w3.org/2000/xmlns/","xmlns:xlink","http://www.w3.org/1999/xlink"),c.appendChild(h),h.appendChild(g("rect",{fill:b.colorDark,width:"1",height:"1",id:"template"}));for(var i=0;d>i;i++)for(var j=0;d>j;j++)if(a.isDark(i,j)){var k=g("use",{x:String(i),y:String(j)});k.setAttributeNS("http://www.w3.org/1999/xlink","href","#template"),h.appendChild(k)}},a.prototype.clear=function(){for(;this._el.hasChildNodes();)this._el.removeChild(this._el.lastChild)},a}(),p="svg"===document.documentElement.tagName.toLowerCase(),q=p?o:m()?function(){function a(){this._elImage.src=this._elCanvas.toDataURL("image/png"),this._elImage.style.display="block",this._elCanvas.style.display="none"}function d(a,b){var c=this;if(c._fFail=b,c._fSuccess=a,null===c._bSupportDataURI){var d=document.createElement("img"),e=function(){c._bSupportDataURI=!1,c._fFail&&_fFail.call(c)},f=function(){c._bSupportDataURI=!0,c._fSuccess&&c._fSuccess.call(c)};return d.onabort=e,d.onerror=e,d.onload=f,d.src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==",void 0}c._bSupportDataURI===!0&&c._fSuccess?c._fSuccess.call(c):c._bSupportDataURI===!1&&c._fFail&&c._fFail.call(c)}if(this._android&&this._android<=2.1){var b=1/window.devicePixelRatio,c=CanvasRenderingContext2D.prototype.drawImage;CanvasRenderingContext2D.prototype.drawImage=function(a,d,e,f,g,h,i,j){if("nodeName"in a&&/img/i.test(a.nodeName))for(var l=arguments.length-1;l>=1;l--)arguments[l]=arguments[l]*b;else"undefined"==typeof j&&(arguments[1]*=b,arguments[2]*=b,arguments[3]*=b,arguments[4]*=b);c.apply(this,arguments)}}var e=function(a,b){this._bIsPainted=!1,this._android=n(),this._htOption=b,this._elCanvas=document.createElement("canvas"),this._elCanvas.width=b.width,this._elCanvas.height=b.height,a.appendChild(this._elCanvas),this._el=a,this._oContext=this._elCanvas.getContext("2d"),this._bIsPainted=!1,this._elImage=document.createElement("img"),this._elImage.style.display="none",this._el.appendChild(this._elImage),this._bSupportDataURI=null};return e.prototype.draw=function(a){var b=this._elImage,c=this._oContext,d=this._htOption,e=a.getModuleCount(),f=d.width/e,g=d.height/e,h=Math.round(f),i=Math.round(g);b.style.display="none",this.clear();for(var j=0;e>j;j++)for(var k=0;e>k;k++){var l=a.isDark(j,k),m=k*f,n=j*g;c.strokeStyle=l?d.colorDark:d.colorLight,c.lineWidth=1,c.fillStyle=l?d.colorDark:d.colorLight,c.fillRect(m,n,f,g),c.strokeRect(Math.floor(m)+.5,Math.floor(n)+.5,h,i),c.strokeRect(Math.ceil(m)-.5,Math.ceil(n)-.5,h,i)}this._bIsPainted=!0},e.prototype.makeImage=function(){this._bIsPainted&&d.call(this,a)},e.prototype.isPainted=function(){return this._bIsPainted},e.prototype.clear=function(){this._oContext.clearRect(0,0,this._elCanvas.width,this._elCanvas.height),this._bIsPainted=!1},e.prototype.round=function(a){return a?Math.floor(1e3*a)/1e3:a},e}():function(){var a=function(a,b){this._el=a,this._htOption=b};return a.prototype.draw=function(a){for(var b=this._htOption,c=this._el,d=a.getModuleCount(),e=Math.floor(b.width/d),f=Math.floor(b.height/d),g=['<table style="border:0;border-collapse:collapse;">'],h=0;d>h;h++){g.push("<tr>");for(var i=0;d>i;i++)g.push('<td style="border:0;border-collapse:collapse;padding:0;margin:0;width:'+e+"px;height:"+f+"px;background-color:"+(a.isDark(h,i)?b.colorDark:b.colorLight)+';"></td>');g.push("</tr>")}g.push("</table>"),c.innerHTML=g.join("");var j=c.childNodes[0],k=(b.width-j.offsetWidth)/2,l=(b.height-j.offsetHeight)/2;k>0&&l>0&&(j.style.margin=l+"px "+k+"px")},a.prototype.clear=function(){this._el.innerHTML=""},a}();QRCode=function(a,b){if(this._htOption={width:256,height:256,typeNumber:4,colorDark:"#000000",colorLight:"#ffffff",correctLevel:d.H},"string"==typeof b&&(b={text:b}),b)for(var c in b)this._htOption[c]=b[c];"string"==typeof a&&(a=document.getElementById(a)),this._android=n(),this._el=a,this._oQRCode=null,this._oDrawing=new q(this._el,this._htOption),this._htOption.text&&this.makeCode(this._htOption.text)},QRCode.prototype.makeCode=function(a){this._oQRCode=new b(r(a,this._htOption.correctLevel),this._htOption.correctLevel),this._oQRCode.addData(a),this._oQRCode.make(),this._el.title=a,this._oDrawing.draw(this._oQRCode),this.makeImage()},QRCode.prototype.makeImage=function(){"function"==typeof this._oDrawing.makeImage&&(!this._android||this._android>=3)&&this._oDrawing.makeImage()},QRCode.prototype.clear=function(){this._oDrawing.clear()},QRCode.CorrectLevel=d}();]]>
                </script>
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
                    <!-- border-collapse: collapse; -->
                    background-color:
                    }
                    #notesTableTd {
                    border-width: 0px;
                    border-spacing:;
                    border-style: inset;
                    border-color: black;
                    border-collapse: collapse;
                    background-color:
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
                </style>
                <title>e-Belge</title>
            </head>

            <body style="width:800px;">
                <xsl:for-each select="$XML">



                    <!-- GONDERICI - EARSIV LOGO - FIRMA LOGO TABLOSU -->
                    <table style="width: 100%;">
                        <tbody>
                            <tr>
                                <td style="width: 40%;">
                                    <hr/>


                                    <!-- GONDERICI TABLOSU -->
                                    <table style="width: 100%;">
                                        <tbody>
                                            <tr align="left">
                                                <td align="left">
                                                    <xsl:if test="count(//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentTypeCode[text()='SUBE_UNVAN']) &gt;= 1">
                                                        <xsl:for-each select="//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentTypeCode[text()='SUBE_UNVAN']">
                                                            <xsl:value-of select="../cbc:DocumentType"/>
                                                            <br />
                                                        </xsl:for-each>
                                                    </xsl:if>
                                                    <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
                                                        <xsl:if test="$varBranchName= '' or count(//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='VKN']) &gt;= 1">
                                                            <xsl:if test="cac:PartyName">
                                                                <xsl:value-of select="cac:PartyName/cbc:Name"/>
                                                                <br/>
                                                            </xsl:if>
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
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                            <tr align="left">
                                                <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
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
                                                            <xsl:if test="cbc:Room">
                                                                <xsl:text> Kapı No:</xsl:text>
                                                                <xsl:for-each select="cbc:Room">
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
                                            <xsl:if test="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:Telephone or //n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:Telefax">
                                                <tr align="left">
                                                    <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
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
                                            <xsl:for-each select="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cbc:WebsiteURI">
                                                <tr align="left">
                                                    <td>
                                                        <xsl:text>Web Sitesi: </xsl:text>
                                                        <xsl:value-of select="."/>
                                                    </td>
                                                </tr>
                                            </xsl:for-each>
                                            <xsl:for-each select="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:Contact/cbc:ElectronicMail">
                                                <tr align="left">
                                                    <td>
                                                        <xsl:text>E-Posta: </xsl:text>
                                                        <xsl:value-of select="."/>
                                                    </td>
                                                </tr>
                                            </xsl:for-each>
                                            <tr align="left">
                                                <xsl:for-each select="n1:Invoice/cac:AccountingSupplierParty/cac:Party">
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
                                            <xsl:for-each select="//n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification">
                                                <tr align="left">
                                                    <xsl:choose>
                                                        <xsl:when test="cbc:ID/@schemeID = 'MERSISNO'">
                                                            <td>
                                                                <xsl:text>Mersis No: </xsl:text>
                                                                <xsl:value-of select="cbc:ID"/>
                                                            </td>
                                                        </xsl:when>
                                                        <xsl:when test="cbc:ID/@schemeID = 'TICARETSICILNO'">
                                                            <td>
                                                                <xsl:text>Ticaret Sicil No: </xsl:text>
                                                                <xsl:value-of select="cbc:ID"/>
                                                            </td>
                                                        </xsl:when>
                                                        <xsl:otherwise>
                                                            <td>
                                                                <xsl:value-of select="cbc:ID/@schemeID"/>
                                                                <xsl:text>: </xsl:text>
                                                                <xsl:value-of select="cbc:ID"/>
                                                            </td>
                                                        </xsl:otherwise>
                                                    </xsl:choose>
                                                </tr>
                                            </xsl:for-each>

                                            <xsl:for-each select="//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentType[text()='MESUL_MUDUR_AD_SOYAD']">
                                                <tr align="left">
                                                    <td align="left">
                                                        <xsl:if test="../cbc:DocumentType='MESUL_MUDUR_AD_SOYAD'">
                                                            <xsl:text>Mesul Müdür: </xsl:text>
                                                        </xsl:if>
                                                        <xsl:if test="../cbc:DocumentType='MESUL_MUDUR_AD_SOYAD'">
                                                            <xsl:value-of select="../cbc:DocumentTypeCode"/>
                                                        </xsl:if>
                                                    </td>
                                                </tr>
                                            </xsl:for-each>

                                            <xsl:if test="count(//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentType[text()='MESUL_MUDUR_BELGE_NO']) &gt;= 1 and count(//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentType[text()='MESUL_MUDUR_RUHSATNAME_TARIH']) &gt;= 1">
                                                <tr align="left">
                                                    <td align="left">
                                                        <xsl:for-each select="//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentType[text()='MESUL_MUDUR_RUHSATNAME_TARIH']">
                                                            <xsl:if test="../cbc:DocumentType='MESUL_MUDUR_RUHSATNAME_TARIH'">
                                                                <xsl:text>Ruhsat Tarihi: </xsl:text>
                                                            </xsl:if>
                                                            <xsl:if test="../cbc:DocumentType='MESUL_MUDUR_RUHSATNAME_TARIH'">
                                                                <xsl:value-of select="substring(../cbc:DocumentTypeCode,9,2)"/>-<xsl:value-of select="substring(../cbc:DocumentTypeCode,6,2)"/>-<xsl:value-of select="substring(../cbc:DocumentTypeCode,1,4)"/>
                                                            </xsl:if>
                                                        </xsl:for-each>
                                                        <xsl:for-each select="//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentType[text()='MESUL_MUDUR_BELGE_NO']">
                                                            <xsl:if test="../cbc:DocumentType='MESUL_MUDUR_BELGE_NO'">
                                                                <xsl:text> - Belge No: </xsl:text>
                                                            </xsl:if>
                                                            <xsl:if test="../cbc:DocumentType='MESUL_MUDUR_BELGE_NO'">
                                                                <xsl:value-of select="../cbc:DocumentTypeCode"/>
                                                            </xsl:if>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                        </tbody>
                                    </table>
                                    <hr/>
                                </td>


                                <!-- E-ARSIV FATURA LOGO -->
                                <td style="width: 20%; text-align: center;">
                                    <img style="width:91px;" align="middle" alt="E-Fatura Logo"
                                         src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="/>
                                    <h1 style="text-align: center;">
                                        <span style="font-weight:bold; ">
                                            <xsl:choose>
                                                <xsl:when test="//n1:Invoice/cbc:ProfileID='EARSIVFATURA' or //n1:Invoice/cbc:ProfileID='EBELGE'">
                                                    <xsl:text>e-Arşiv Fatura</xsl:text>
                                                </xsl:when>
                                                <xsl:when test="//n1:Invoice/cbc:ProfileID='EGIDERPUSULASI'">
                                                    <xsl:text>e-Gider Pusulası</xsl:text>
                                                </xsl:when>
                                                <xsl:otherwise>
                                                    <xsl:text>e-FATURA</xsl:text>
                                                </xsl:otherwise>
                                            </xsl:choose>
                                        </span>
                                    </h1>
                                </td>

                                <!-- FIRMA LOGOSU -->
                                <td style="width: 40%; text-align: center;">
                                    <table style="width: 100%;">
                                        <tbody>
                                            <tr align="right">
                                                <td align="right">
                                                    <div id="qrcode" style="float:right; margin-top: -6px;"/>
                                                </td>
                                            </tr>
                                           
                                        </tbody>
                                    </table>
                                </td>
                            </tr>
                        </tbody>
                    </table>


                    <!-- ALICI - IMZA - FATURA BILGILERI TABLOSU -->
                    <table>
                        <tbody>
                            <tr>
                                <td style="width: 40%;">
                                    <table id="customerPartyTable" align="left" border="0">
                                        <tbody>
                                            <tr>
                                                <td>
                                                    <hr/>
                                                    <table align="center" border="0">
                                                        <tbody>
                                                            <tr>
                                                                <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                                                                    <td style="width:469px; " align="left">
                                                                        <span style="font-weight:bold; ">
                                                                            <xsl:text>SAYIN</xsl:text>
                                                                        </span>
                                                                    </td>
                                                                </xsl:for-each>
                                                            </tr>
                                                            <tr>
                                                                <xsl:choose>
                                                                    <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='TAXFREE']">
                                                                        <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                            <xsl:call-template name="Party_Title">
                                                                                <xsl:with-param name="PartyType">TAXFREE</xsl:with-param>
                                                                            </xsl:call-template>
                                                                        </xsl:for-each>
                                                                    </xsl:when>
                                                                    <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and starts-with(text(), 'EXPORT')]">
                                                                        <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                            <xsl:call-template name="Party_Title">
                                                                                <xsl:with-param name="PartyType">EXPORT</xsl:with-param>
                                                                            </xsl:call-template>
                                                                        </xsl:for-each>
                                                                    </xsl:when>
                                                                    <xsl:otherwise>
                                                                        <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                                                                            <xsl:call-template name="Party_Title">
                                                                                <xsl:with-param name="PartyType">OTHER</xsl:with-param>
                                                                            </xsl:call-template>
                                                                        </xsl:for-each>
                                                                    </xsl:otherwise>
                                                                </xsl:choose>
                                                            </tr>
                                                            <xsl:choose>
                                                                <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and text()='TAXFREE']">
                                                                    <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                        <tr>
                                                                            <xsl:call-template name="Party_Adress">
                                                                                <xsl:with-param name="PartyType">TAXFREE</xsl:with-param>
                                                                            </xsl:call-template>
                                                                        </tr>
                                                                        <xsl:call-template name="Party_Other">
                                                                            <xsl:with-param name="PartyType">TAXFREE</xsl:with-param>
                                                                        </xsl:call-template>
                                                                    </xsl:for-each>
                                                                </xsl:when>
                                                                <xsl:when test="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE' and starts-with(text(), 'EXPORT')]">
                                                                    <xsl:for-each select="n1:Invoice/cac:BuyerCustomerParty/cac:Party">
                                                                        <tr>
                                                                            <xsl:call-template name="Party_Adress">
                                                                                <xsl:with-param name="PartyType">EXPORT</xsl:with-param>
                                                                            </xsl:call-template>
                                                                        </tr>
                                                                        <xsl:call-template name="Party_Other">
                                                                            <xsl:with-param name="PartyType">EXPORT</xsl:with-param>
                                                                        </xsl:call-template>
                                                                    </xsl:for-each>
                                                                </xsl:when>
                                                                <xsl:otherwise>
                                                                    <xsl:for-each select="n1:Invoice/cac:AccountingCustomerParty/cac:Party">
                                                                        <tr>
                                                                            <xsl:call-template name="Party_Adress">
                                                                                <xsl:with-param name="PartyType">OTHER</xsl:with-param>
                                                                            </xsl:call-template>
                                                                        </tr>
                                                                        <xsl:call-template name="Party_Other">
                                                                            <xsl:with-param name="PartyType">OTHER</xsl:with-param>
                                                                        </xsl:call-template>
                                                                    </xsl:for-each>
                                                                </xsl:otherwise>
                                                            </xsl:choose>

                                                            <xsl:if test="$varSenaryoName='1' and not($varVknComp='1')">
                                                                <tr align="left">
                                                                    <td style="width:469px; " align="left">
                                                                        <span style="font-weight:bold; ">
                                                                            <br> </br>
                                                                            <xsl:text> Ödeme Yapacak Kurum </xsl:text>
                                                                        </span>
                                                                    </td>
                                                                </tr>
                                                                <tr align="left">
                                                                    <td>
                                                                        <xsl:text> VKN: </xsl:text>
                                                                        <xsl:value-of select="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='VKN']"/>
                                                                    </td>
                                                                </tr>
                                                                <tr align="left">
                                                                    <td>
                                                                        <xsl:text> Ünvan: </xsl:text>
                                                                        <xsl:value-of select="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyName/cbc:Name"/>
                                                                    </td>
                                                                </tr>
                                                                <tr align="left">
                                                                    <td>
                                                                        <xsl:text> Adres: </xsl:text>
                                                                        <xsl:value-of select="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PostalAddress/cbc:CityName"/>
                                                                        <xsl:text>/ </xsl:text>
                                                                        <xsl:value-of select="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PostalAddress/cac:Country/cbc:Name"/>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>
                                                        </tbody>
                                                    </table>
                                                    <hr/>
                                                </td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </td>

                                <td style="width: 30%; text-align: center;">
								<div id="imza">
                               </div>
							   </td>
                                <td style="width: 30%;">
												<div id="company_logo"></div>

                                    <table style="width:100%; border-collapse: collapse;">
                                        
                                        <tbody>
                                            <tr>
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Özelleştirme No:</xsl:text>
                                                    </span>
                                                </td>
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <xsl:for-each select="n1:Invoice/cbc:CustomizationID">
                                                        <xsl:apply-templates/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                            <tr style="height:13px; ">
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Senaryo:</xsl:text>
                                                    </span>
                                                </td>
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <xsl:for-each select="n1:Invoice/cbc:ProfileID">
                                                        <xsl:apply-templates/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                            <xsl:if test="not(//n1:Invoice/cbc:ProfileID='EGIDERPUSULASI')">
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Fatura Tipi:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="n1:Invoice/cbc:InvoiceTypeCode">
                                                            <xsl:apply-templates/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>

                                            <xsl:if test="//n1:Invoice/cbc:AccountingCost">
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>İlave Fatura Tipi:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="n1:Invoice/cbc:AccountingCost">
                                                            <xsl:apply-templates/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <tr style="height:13px; ">
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <xsl:choose>
                                                        <xsl:when test="//n1:Invoice/cbc:ProfileID='EGIDERPUSULASI'">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Pusula No:</xsl:text>
                                                            </span>
                                                        </xsl:when>
                                                        <xsl:otherwise>
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Fatura No:</xsl:text>
                                                            </span>
                                                        </xsl:otherwise>
                                                    </xsl:choose>
                                                </td>
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <xsl:for-each select="n1:Invoice/cbc:ID">
                                                        <xsl:apply-templates/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                            <tr style="height:13px; ">
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <xsl:choose>
                                                        <xsl:when test="//n1:Invoice/cbc:ProfileID='EGIDERPUSULASI'">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Pusula Tarihi:</xsl:text>
                                                            </span>
                                                        </xsl:when>
                                                        <xsl:otherwise>
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Fatura Tarihi:</xsl:text>
                                                            </span>
                                                        </xsl:otherwise>
                                                    </xsl:choose>
                                                </td>
                                                <td id="invoice-info-td" style="width: 50%;">
                                                    <xsl:for-each select="n1:Invoice/cbc:IssueDate">
                                                        <xsl:apply-templates select="."/>
                                                        <xsl:text>&#160;</xsl:text>
                                                        <xsl:value-of select="substring(../cbc:IssueTime,1,5)"/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                            <xsl:for-each select="//n1:Invoice/cac:AdditionalDocumentReference/cbc:DocumentTypeCode[text()='MUKELLEF_KODU' or text()='MUKELLEF_ADI' or text()='DOSYA_NO']">
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold;">
                                                            <xsl:if test="../cbc:DocumentTypeCode='MUKELLEF_KODU'">
                                                                <xsl:text>Mükellef Kodu:</xsl:text>
                                                            </xsl:if>
                                                            <xsl:if test="../cbc:DocumentTypeCode='MUKELLEF_ADI'">
                                                                <xsl:text>Mükellef Adı:</xsl:text>
                                                            </xsl:if>
                                                            <xsl:if test="../cbc:DocumentTypeCode='DOSYA_NO'">
                                                                <xsl:text>Dosya No:</xsl:text>
                                                            </xsl:if>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:value-of select="../cbc:DocumentType"/>
                                                    </td>
                                                </tr>
                                            </xsl:for-each>
                                            <xsl:if test="(//n1:Invoice/cbc:AccountingCost) and (//n1:Invoice/cac:InvoicePeriod)">
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold;">
                                                            <xsl:text>Dönem Başlangıcı:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="//n1:Invoice/cac:InvoicePeriod">
                                                            <xsl:apply-templates select="cbc:StartDate"/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold;">
                                                            <xsl:text>Dönem Bitişi:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="//n1:Invoice/cac:InvoicePeriod">
                                                            <xsl:apply-templates select="cbc:EndDate"/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:for-each select="n1:Invoice/cac:DespatchDocumentReference">
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>İrsaliye No:</xsl:text>
                                                        </span>
                                                        <xsl:text>&#160;</xsl:text>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:value-of select="cbc:ID"/>
                                                    </td>
                                                </tr>
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>İrsaliye Tarihi:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="cbc:IssueDate">
                                                            <xsl:apply-templates select="."/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:for-each>
                                            <xsl:if test="//n1:Invoice/cbc:paymentDueDate">
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Son Ödeme Tarihi:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="n1:Invoice/cbc:paymentDueDate">
                                                            <xsl:apply-templates/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:if test="//n1:Invoice/cac:OrderReference">
                                                <tr style="height:13px">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Sipariş No:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="n1:Invoice/cac:OrderReference/cbc:ID">
                                                            <xsl:apply-templates/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:if test="//n1:Invoice/cac:OrderReference/cbc:IssueDate">
                                                <tr style="height:13px">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Sipariş Tarihi:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="n1:Invoice/cac:OrderReference/cbc:IssueDate">
                                                            <xsl:apply-templates select="."/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:if test="n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='VKN' and text()='7350019759']">
                                                <tr style="height:13px">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Sipariş Sorumlusu:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:value-of select="$varsiparissorumlusu"/>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:for-each select="n1:Invoice/cac:ReceiptDocumentReference">
                                                <tr style="height:13px; ">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Mal Kabul No:</xsl:text>
                                                        </span>
                                                        <xsl:text>&#160;</xsl:text>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:value-of select="cbc:ID"/>
                                                    </td>
                                                </tr>
                                            </xsl:for-each>
                                            <xsl:for-each select="n1:Invoice/cac:TaxRepresentativeParty/cac:PartyIdentification/cbc:ID[@schemeID='ARACIKURUMVKN']">
                                                <tr>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Aracı Kurum VKN:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:value-of select="."/>
                                                    </td>
                                                </tr>
                                                <tr>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Aracı Kurum Unvan:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:value-of select="../../cac:PartyName/cbc:Name"/>
                                                    </td>
                                                </tr>
                                            </xsl:for-each>
                                            <!--                                            <xsl:if test="//n1:Invoice/cac:PaymentMeans/cbc:PaymentMeansCode">-->
                                            <!--                                                <tr style="height:13px">-->
                                            <!--                                                    <td id="invoice-info-td" style="width: 50%;">-->
                                            <!--                                                        <span style="font-weight:bold; ">-->
                                            <!--                                                            <xsl:text>Ödeme Şekli:</xsl:text>-->
                                            <!--                                                        </span>-->
                                            <!--                                                    </td>-->
                                            <!--                                                    <td id="invoice-info-td" style="width: 50%;">-->
                                            <!--                                                        <xsl:for-each select="n1:Invoice/cac:PaymentMeans/cbc:PaymentMeansCode">-->
                                            <!--                                                            <xsl:call-template name="PaymentMeansCode">-->
                                            <!--                                                                <xsl:with-param name="PaymentMeansCodeType">-->
                                            <!--                                                                    <xsl:value-of select="."/>-->
                                            <!--                                                                </xsl:with-param>-->
                                            <!--                                                            </xsl:call-template>-->
                                            <!--                                                        </xsl:for-each>-->
                                            <!--                                                    </td>-->
                                            <!--                                                </tr>-->
                                            <!--                                            </xsl:if>-->
                                            <xsl:if test="//n1:Invoice/cac:PaymentMeans/cbc:PaymentDueDate">
                                                <tr style="height:13px">
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Ödeme Tarihi:</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td id="invoice-info-td" style="width: 50%;">
                                                        <xsl:for-each select="n1:Invoice/cac:PaymentMeans/cbc:PaymentDueDate">
                                                            <xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                        </tbody>
                                    </table>
                                </td>
                            </tr>
                        </tbody>
                    </table>



                    <!-- ETTN SATIRI -->
                    <table style="width: 40%; margin-bottom: 5px;">
                        <tr>
                            <td style="width: 100%;">
                                <span style="font-weight:bold; ">
                                    <xsl:text>ETTN:</xsl:text>
                                </span>
                                &#160;
                                <xsl:for-each select="n1:Invoice/cbc:UUID">
                                    <xsl:apply-templates/>
                                </xsl:for-each>
                            </td>
                        </tr>
                    </table>

                    <!-- URUNLER SATIRI (TABLOSU) -->
                    <table style="width: 100%; border-collapse: collapse; border-style: solid; border-width: 2px;">
                        <tbody>
                            <tr>
                                <td id="invoice-line-td" style="width:3%">
                                    <span style="font-weight:bold;">
                                        <xsl:text>Sıra No</xsl:text>
                                    </span>
                                </td>
                                <xsl:choose>
                                    <xsl:when test="$varItemCode &gt; 0">
                                        <td id="invoice-line-td" style="width:7%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Ürün Kodu</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:when>
                                    <xsl:otherwise>
                                    </xsl:otherwise>
                                </xsl:choose>
                                <xsl:choose>
                                    <xsl:when test="//n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='VKN' and text()='7350019759']">
                                        <td id="invoice-line-td" style="width:7%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Sipariş Satır No</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:when>
                                </xsl:choose>

                                <xsl:if test="$varfaturatipi='SGK'">
                                    <td id="invoice-line-td" style="width:80%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Açıklama</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>
                                <xsl:if test="not($varfaturatipi='SGK')">
                                    <xsl:if test="not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                        <td id="invoice-line-td" style="width:20%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Mal Hizmet</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>

                                    <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                                        <td id="invoice-line-td" style="width:10.6%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>İade Edilen Mal Hizmet</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>

                                    <td id="invoice-line-td" style="width:7.4%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Miktar</xsl:text>
                                        </span>
                                    </td>
                                    <xsl:if test="$varEtiketFiyati='1'">
                                        <td id="invoice-line-td" class="table-background table-header" style="width:10%; border-color: #c9c9c9">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Etiket Fiyatı</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>
                                    <xsl:if test="$varDepocuFiyati='1'">
                                        <td id="invoice-line-td" class="table-background table-header" style="width:9%; border-color: #c9c9c9">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Depocu Fiyatı</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>

                                    <td id="invoice-line-td" class="table-background table-header" style="width:9%; border-color: #c9c9c9">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Birim Fiyat</xsl:text>
                                        </span>
                                    </td>
                                    <xsl:if test="$varEczaciKar='1'">
                                        <td id="invoice-line-td" class="table-background table-header" style="width:10%; border-color: #c9c9c9">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Eczacı Kâr Oranı.</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>

                                    <xsl:if test="$varKurumIskonto='1'">
                                        <td id="invoice-line-td" class="table-background table-header" style="width:10%; border-color: #c9c9c9">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Kurum İskontosu</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>

                                    <xsl:if test="$varVade='1'">
                                        <td id="invoice-line-td" class="table-background table-header" style="width:10%; border-color: #c9c9c9">
                                            <span style="font-weight:bold;">
                                                <xsl:text>Vade Tarihi</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>
                                    <xsl:if test="$varAllowanceRate &gt; 0">
                                        <td id="invoice-line-td" style="width:7%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>İskonto/ Arttırım Oranı</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>
                                    <xsl:if test="$varAllowanceAmount &gt; 0">
                                        <td id="invoice-line-td" style="width:9%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>İskonto/ Arttırım Tutarı</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>
                                    <xsl:if test="$varAllowanceReason &gt; 0">
                                        <td id="invoice-line-td" style="width:9%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>İskonto/ Arttırım Nedeni</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>
                                    <xsl:if test="not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                        <td id="invoice-line-td" style="width:7%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>KDV Oranı</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>
                                    <xsl:if test="not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                        <td id="invoice-line-td" style="width:10%">
                                            <span style="font-weight:bold;">
                                                <xsl:text>KDV Tutarı</xsl:text>
                                            </span>
                                        </td>
                                    </xsl:if>
                                    <xsl:if test="not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                 
                                    
                                    </xsl:if>
                                    <xsl:if test="$varLineExplanation &gt; 0">
                             
                                    </xsl:if>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>İade Edilen Mal Oranı (%)</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>İadeye Konu KDV Tutarı</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>
                                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                                    <td id="invoice-line-td" style="width:17%; ">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Diğer Vergiler</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>
                                <xsl:if test="not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Mal Hizmet Tutarı</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>İadeye Konu İşlem Bedeli</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Alıştaki Tevkifatsız KDV Tutarı</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:ProfileID='EARSIVFATURA' and //n1:Invoice/cbc:InvoiceTypeCode='ISTISNA'">
                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>GTIP</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:ProfileID='HKS' or /n1:Invoice/cbc:InvoiceTypeCode='HKSSATIS' or /n1:Invoice/cbc:InvoiceTypeCode='HKSKOMISYONCU'">
                                    <td id="invoice-line-td" style="width:5%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Künye Numarası</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:ProfileID='HKS' and /n1:Invoice/cbc:InvoiceTypeCode='SATIS'">
                                    <td id="invoice-line-td" style="width:5%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Mal Sahibi VKN/TCKN</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:5%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Mal Sahibi Ad/Soyad</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='HKSSATIS'">
                                    <td id="invoice-line-td" style="width:5%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Mal Sahibi VKN/TCKN</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:5%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Mal Sahibi Ad/Soyad</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>

                                <xsl:if test="//n1:Invoice/cbc:ProfileID='IHRACAT' or //n1:Invoice/cbc:ProfileID='OZELFATURA'">
                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Teslim Şartı</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Eşya Kap Cinsi</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Kap No</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Kap Adet</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Teslim/Bedel Ödeme Yeri</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Gönderilme Şekli</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>GTİP</xsl:text>
                                        </span>
                                    </td>

                                    <td id="invoice-line-td" style="width:10.6%">
                                        <span style="font-weight:bold;">
                                            <xsl:text>Byn. Edilen Kıymet Değeri</xsl:text>
                                        </span>
                                    </td>
                                </xsl:if>
                            </tr>

                            <xsl:if test="count(//n1:Invoice/cac:InvoiceLine) &gt;= 1">
                                <xsl:for-each select="//n1:Invoice/cac:InvoiceLine">
                                    <xsl:apply-templates select="."/>
                                </xsl:for-each>
                            </xsl:if>

                            <xsl:if test="count(//n1:Invoice/cac:InvoiceLine) &lt; 1">
                                <xsl:choose>
                                    <xsl:when test="//n1:Invoice/cac:InvoiceLine[1]">
                                        <xsl:apply-templates
                                                select="//n1:Invoice/cac:InvoiceLine[1]"/>
                                    </xsl:when>
                                    <xsl:otherwise>
                                        <xsl:apply-templates select="//n1:Invoice"/>
                                    </xsl:otherwise>
                                </xsl:choose>
                            </xsl:if>
                        </tbody>
                    </table>
                </xsl:for-each>


                <table id="budgetContainerTable" table-layout="fixed" width="800px">
                    <tbody>
                        <tr>
                            <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='HKSKOMISYONCU' or //n1:Invoice/cbc:InvoiceTypeCode='KOMISYONCU'">
                                <td align="left" valign="top" width="300px">
                                    <table>
                                        <tbody>
                                            <xsl:for-each select="n1:Invoice/cac:AllowanceCharge">
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSKOMISYON'">
                                                    <tr align="left" border="0">
                                                        <td align="left" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Masraflar:</xsl:text>
                                                            </span>
                                                        </td>

                                                    </tr>
                                                    <tr align="left">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Komisyon - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSKOMISYONKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Komisyon KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSNAVLUN' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Navlun - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSNAVLUNKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Navlun KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSHAMMALIYE' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Hammaliye - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSHAMMALIYEKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Hammaliye KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSNAKLIYE' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Nakliye - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSNAKLIYEKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Nakliye KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSGVTEVKIFAT' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>G.V. Tevkifat - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSBAGKURTEVKIFAT' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Bağkur Tevkifat - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSRUSUM' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Rüsum - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSRUSUMKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Rüsum KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSTICBORSASI' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Ticaret Borsası - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSTICBORSASIKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Ticaret Borsası KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSMILLISAVUNMAFON' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Milli Savunma Fon - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSMSFONKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Milli Savunma Fon KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSDIGERMASRAFLAR' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Diğer Masraflar - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                                <xsl:if test="cbc:AllowanceChargeReason = 'HKSDIGERKDV' and not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                    <tr align="right">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Diğer KDV - %</xsl:text>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:Amount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                            <xsl:for-each
                                                                    select="cbc:MultiplierFactorNumeric">
                                                                <xsl:text> %</xsl:text>
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:if>
                                            </xsl:for-each>

                                        </tbody>
                                    </table>
                                </td>
                            </xsl:if>
                            <td align="right" valign="top">
                                <table>
                                    <tbody>
                                        <xsl:if test="not($varfaturatipi='SGK' and $varoptik='medula')">
                                            <tr align="right">
                                                <td/>
                                                <xsl:if test="not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Mal Hizmet Toplam Tutarı</xsl:text>
                                                    </span>
                                                </td>
                                                </xsl:if>

                                                <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>İadeye Konu İşlem Bedeli Tutarı</xsl:text>
                                                    </span>
                                                </td>
                                                </xsl:if>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount">
                                                        <xsl:call-template name="Curr_Type"/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
                                            <xsl:if test="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode = '4171'">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Teslim Bedeli</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <xsl:for-each select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount">
                                                            <xsl:call-template name="Curr_Type"/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                        </xsl:for-each>
                                        <xsl:if test="not($varfaturatipi='SGK')">
                                            <tr align="right">
                                                <td/>
                                                <xsl:choose>
                                                    <xsl:when
                                                            test="//n1:Invoice/cac:AllowanceCharge/cbc:ChargeIndicator='true'">
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Toplam Arttırım - </xsl:text>
                                                                <xsl:for-each
                                                                        select="n1:Invoice/cac:AllowanceCharge/cbc:AllowanceChargeReason">
                                                                    <xsl:apply-templates/>
                                                                </xsl:for-each>
                                                            </span>
                                                        </td>
                                                    </xsl:when>
                                                    <xsl:otherwise>
                                                        <td class="lineTableBudgetTd" align="right" width="200px">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:if test="not($varoptik='medikal')">
                                                                    <xsl:text>Toplam İskonto</xsl:text>
                                                                </xsl:if>
                                                                <xsl:if test="$varoptik='medikal'">
                                                                    <xsl:text>Katılım Payı</xsl:text>
                                                                </xsl:if>
                                                            </span>
                                                        </td>
                                                    </xsl:otherwise>
                                                </xsl:choose>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <xsl:if test="$varoptik='medikal' and not($varisitmekatilimpayi='')" >
                                                        <xsl:value-of select="format-number(number($varisitmekatilimpayi), '###.##0,00', 'european')" />
                                                        <xsl:text> TL</xsl:text>
                                                    </xsl:if>
                                                    <xsl:if test="not($varoptik='medikal' and not($varisitmekatilimpayi=''))" >
                                                        <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount">
                                                            <xsl:call-template name="Curr_Type"/>
                                                        </xsl:for-each>
                                                    </xsl:if>
                                                </td>
                                            </tr>
                                        </xsl:if>

                                        <xsl:if test="$varfaturatipi='SGK' and $varoptik='cezaeviEczanem'">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Toplam İskonto</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <span>
                                                        <xsl:value-of select="format-number(number($variskonto), '###.##0,00', 'european')" />
                                                        <xsl:text> TL</xsl:text>
                                                    </span>
                                                </td>
                                            </tr>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>İlaç Farkı</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <span>
                                                        <xsl:value-of select="format-number(number($varilacfarki), '###.##0,00', 'european')" />
                                                        <xsl:text> TL</xsl:text>
                                                    </span>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="$varfaturatipi='SGK' and $varoptik='optik'">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Toplam Katılım Payı</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <span>
                                                        <xsl:value-of select="format-number(number($varkatilimpayi), '###.##0,00', 'european')" />
                                                        <xsl:text> TL</xsl:text>
                                                    </span>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="$varfaturatipi='SGK' and $varoptik='medula'">
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="not($varpsf='undefined' or $varpsf='' or $varfaturatype='CETAS')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:value-of select="$varreceteadedi" /> Adet Reçete PSF Toplamı
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number(number($varpsf), '###.##0,00', 'european')" />
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="not($varpsf='undefined' or $varpsf='' or $varfaturatype='CETAS')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Kamu Kurum İskontosu</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number((number($varpsf)- number($vartutar)), '###.##0,00', 'european')" />
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="not($varfaturatype='CETAS')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:value-of select="$varreceteadedi"/> Adet Reçete Kamu Fiyatı Toplamı
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount">
                                                                <xsl:call-template name="Curr_Type"/>
                                                            </xsl:for-each>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Eczane İskontosu</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <span>
                                                        <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:AllowanceTotalAmount">
                                                            <xsl:call-template name="Curr_Type"/>
                                                        </xsl:for-each>
                                                    </span>
                                                </td>
                                            </tr>
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="not($varfaturatype='CETAS')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Hasta Katılım Payı(%10 - %20)</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number(number($varkatilimpayi), '###.##0,00', 'european')"/>
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="not($varfaturatype='CETAS')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" width="210px" align="right">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Vergiler Dahil Reçete Toplam Tutarı</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number((number($vartutar) -(number($variskonto) + number($varkatilimpayi))), '###.##0,00', 'european')"/>
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="not($varfaturatype='CETAS')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" width="210px" align="right">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Vergiler Hariç Reçete Toplam Tutarı</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number((number($vartutar) -(number($variskonto) + number($varkatilimpayi) + number($varkdv8) + number($varkdv10) + number($varkdv18) + number($varkdv20))), '###.##0,00', 'european')"/>
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:if test="not($varkdv8='undefined' or $varkdv8='')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>KDV (%8)</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number(number($varkdv8), '###.##0,00', 'european')" />
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:if test="not($varkdv10='undefined' or $varkdv10='')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>KDV (%10)</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number(number($varkdv10), '###.##0,00', 'european')" />
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:if test="not($varkdv18='undefined' or $varkdv18='')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>KDV (%18)</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number(number($varkdv18), '###.##0,00', 'european')" />
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <xsl:if test="not($varkdv20='undefined' or $varkdv20='')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>KDV (%20)</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number(number($varkdv20), '###.##0,00', 'european')" />
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="not($varfaturatype='CETAS')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" width="220px" align="right">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Vergiler Dahil Reçete Toplam Tutarı</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                        <span>
                                                            <xsl:value-of select="format-number((number($vartutar) -(number($variskonto) + number($varkatilimpayi))), '###.##0,00', 'european')"/>
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="210px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:choose>
                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:when test="$varfaturatype='CETAS'">
                                                                <xsl:text>Eczane Hizmet Bedeli KDV(%18) Hariç Tutarı</xsl:text>
                                                            </xsl:when>
                                                            <xsl:otherwise>
                                                                <xsl:text>Eczane Hizmet Bedeli KDV Hariç Tutarı</xsl:text>
                                                            </xsl:otherwise>
                                                        </xsl:choose>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <xsl:choose>
                                                        <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                            Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                        <xsl:when test="$varfaturatype='CETAS'">
                                                            <xsl:value-of select="format-number((number($vareczanehizmetbedeli) - number($vareczanekdv18)), '###.##0,00', 'european')" />
                                                        </xsl:when>
                                                        <xsl:otherwise>
                                                            <xsl:value-of select="format-number((number($vareczanehizmetbedeli) - number($vareczanekdv18) - number($vareczanekdv20)), '###.##0,00', 'european')" />
                                                        </xsl:otherwise>
                                                    </xsl:choose>
                                                    <xsl:text> TL</xsl:text>
                                                </td>
                                            </tr>
                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                            Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="$varfaturatype='CETAS'">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="210px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Eczane Hizmet Bedeli KDV(%20) Hariç Tutarı</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                            Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                        <xsl:value-of select="format-number((number($vareczanehizmetbedeli20) - number($vareczanekdv20)), '###.##0,00', 'european')"/>
                                                        <xsl:text> TL</xsl:text>
                                                    </td>
                                                </tr>
                                            </xsl:if>

                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:choose>
                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:when test="$varfaturatype='CETAS'">
                                                                <xsl:text>Eczane Hizmet Bedeli KDV (%18)</xsl:text>
                                                            </xsl:when>
                                                            <xsl:otherwise>
                                                                <xsl:text>KDV (%18)</xsl:text>
                                                            </xsl:otherwise>
                                                        </xsl:choose>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <xsl:value-of select="format-number(number($vareczanekdv18), '###.##0,00', 'european')" />
                                                    <xsl:text> TL</xsl:text>
                                                </td>
                                            </tr>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:choose>
                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:when test="$varfaturatype='CETAS'">
                                                                <xsl:text>Eczane Hizmet Bedeli KDV (%20)</xsl:text>
                                                            </xsl:when>
                                                            <xsl:otherwise>
                                                                <xsl:text>KDV (%20)</xsl:text>
                                                            </xsl:otherwise>
                                                        </xsl:choose>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <xsl:value-of select="format-number(number($vareczanekdv20), '###.##0,00', 'european')" />
                                                    <xsl:text> TL</xsl:text>
                                                </td>
                                            </tr>

                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="210px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:choose>
                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:when test="$varfaturatype='CETAS'">
                                                                <xsl:text>Eczane Hizmet Bedeli KDV(%18) Dahil Tutarı</xsl:text>
                                                            </xsl:when>
                                                            <xsl:otherwise>
                                                                <xsl:text>Eczane Hizmet Bedeli KDV Dahil Tutarı</xsl:text>
                                                            </xsl:otherwise>
                                                        </xsl:choose>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <xsl:value-of select="format-number(number($vareczanehizmetbedeli), '###.##0,00', 'european')" />
                                                    <xsl:text> TL</xsl:text>
                                                </td>
                                            </tr>

                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                            Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                            <xsl:if test="$varfaturatype='CETAS'">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="210px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Eczane Hizmet Bedeli KDV(%20) Dahil Tutarı</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <xsl:value-of select="format-number(number($vareczanehizmetbedeli20), '###.##0,00', 'european')"/>
                                                        <xsl:text> TL</xsl:text>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                        </xsl:if>

                                        <!-- KDV_TAXABLEAMOUNT_REF -->
                                        <!-- EXCHANGERATE_REF -->

                                        <xsl:if test="$varfaturatipi='SGK' and $varoptik='medikalIcmal'">
                                            <xsl:if test="string-length($varkatilimpayi)>0 and not($varkatilimpayi=0)">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" width="211px" align="right">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Katılım Payı</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                        <xsl:value-of select="format-number(number($varkatilimpayi), '###.##0,00', 'european')"/>
                                                        <xsl:text> TL</xsl:text>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                        </xsl:if>

                                        <xsl:if test="not(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
                                            <xsl:if test="not($varfaturatipi='SGK' and $varoptik='medula')">
                                                <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
                                                    <tr align="right">
                                                        <td/>
                                                        <td class="lineTableBudgetTd" width="211px" align="right">
                                                            <span style="font-weight:bold; ">
                                                                <xsl:text>Hesaplanan </xsl:text>
                                                                <xsl:value-of
                                                                        select="cac:TaxCategory/cac:TaxScheme/cbc:Name"/>
                                                                <xsl:if test="../../cbc:InvoiceTypeCode!='OZELMATRAH'">
                                                                    <xsl:text>(%</xsl:text>
                                                                    <xsl:value-of select="cbc:Percent"/>
                                                                    <xsl:text>)</xsl:text>
                                                                </xsl:if>
                                                            </span>
                                                        </td>
                                                        <td class="lineTableBudgetTd" style="width:82px; "
                                                            align="right">
                                                            <xsl:for-each select="cac:TaxCategory/cac:TaxScheme">
                                                                <xsl:text> </xsl:text>
                                                                <xsl:value-of
                                                                        select="format-number(../../cbc:TaxAmount, '###.##0,00', 'european')"/>
                                                                <xsl:if test="../../cbc:TaxAmount/@currencyID">
                                                                    <xsl:text> </xsl:text>
                                                                    <xsl:if test="../../cbc:TaxAmount/@currencyID = 'TRL' or ../../cbc:TaxAmount/@currencyID = 'TRY'">
                                                                        <xsl:text>TL</xsl:text>
                                                                    </xsl:if>
                                                                    <xsl:if test="../../cbc:TaxAmount/@currencyID != 'TRL' and ../../cbc:TaxAmount/@currencyID != 'TRY'">
                                                                        <xsl:value-of
                                                                                select="../../cbc:TaxAmount/@currencyID"/>
                                                                    </xsl:if>
                                                                </xsl:if>
                                                            </xsl:for-each>
                                                        </td>
                                                    </tr>
                                                </xsl:for-each>
                                            </xsl:if>
                                        </xsl:if>
                                        <xsl:if test="(//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE')">
<!--                                            <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">-->
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" width="211px" align="right">
                                                        <span style="font-weight:bold; ">
                                                                <xsl:text>İadeye Konu KDV </xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                        <xsl:text> </xsl:text>
                                                        <xsl:value-of
                                                                select="format-number(sum(//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=0015]/cbc:TaxAmount), '###.##0,00', 'european')"/>
                                                        <xsl:if test="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount/@currencyID">
                                                            <xsl:text> </xsl:text>
                                                            <xsl:if test="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount/@currencyID = 'TRL' or n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount/@currencyID = 'TRY'">
                                                                <xsl:text>TL</xsl:text>
                                                            </xsl:if>
                                                            <xsl:if test="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount/@currencyID != 'TRL' and n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount/@currencyID != 'TRY'">
                                                                <xsl:value-of
                                                                        select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cbc:TaxAmount/@currencyID"/>
                                                            </xsl:if>
                                                        </xsl:if>
                                                    </td>
                                                </tr>
<!--                                            </xsl:for-each>-->
                                        </xsl:if>
                                        <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
                                            <xsl:if test="cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode = '4171'">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>KDV Matrahı</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <xsl:value-of
                                                                select="format-number(sum(//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=0015]/cbc:TaxableAmount), '###.##0,00', 'european')"/>
                                                        <xsl:if
                                                                test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID">
                                                            <xsl:text> </xsl:text>
                                                            <xsl:if
                                                                    test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID = 'TRL' or //n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID = 'TRY'">
                                                                <xsl:text>TL</xsl:text>
                                                            </xsl:if>
                                                            <xsl:if
                                                                    test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID != 'TRL' and //n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID != 'TRY'">
                                                                <xsl:value-of
                                                                        select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount/@currencyID"
                                                                />
                                                            </xsl:if>
                                                        </xsl:if>
                                                    </td>
                                                </tr>
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Tevkifat Dahil Toplam Tutar</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <xsl:for-each select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount">
                                                            <xsl:call-template name="Curr_Type"/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Tevkifat Hariç Toplam Tutar</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <xsl:for-each select="//n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount">
                                                            <xsl:call-template name="Curr_Type"/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                        </xsl:for-each>
                                        <xsl:for-each select="n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="211px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Hesaplanan KDV Tevkifat</xsl:text>
                                                        <xsl:text>(%</xsl:text>
                                                        <xsl:value-of select="cbc:Percent"/>
                                                        <xsl:text>)</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:for-each select="cac:TaxCategory/cac:TaxScheme">
                                                        <xsl:text> </xsl:text>
                                                        <xsl:value-of
                                                                select="format-number(../../cbc:TaxAmount, '###.##0,00', 'european')"/>
                                                        <xsl:if test="../../cbc:TaxAmount/@currencyID">
                                                            <xsl:text> </xsl:text>
                                                            <xsl:if test="../../cbc:TaxAmount/@currencyID = 'TRL' or ../../cbc:TaxAmount/@currencyID = 'TRY'">
                                                                <xsl:text>TL</xsl:text>
                                                            </xsl:if>
                                                            <xsl:if test="../../cbc:TaxAmount/@currencyID != 'TRL' and ../../cbc:TaxAmount/@currencyID != 'TRY'">
                                                                <xsl:value-of select="../../cbc:TaxAmount/@currencyID"/>
                                                            </xsl:if>
                                                        </xsl:if>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                        </xsl:for-each>
                                        <xsl:if test="sum(n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:TaxableAmount)>0">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="211px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Tevkifata Tabi İşlem Tutarı</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:value-of
                                                            select="format-number(sum(n1:Invoice/cac:InvoiceLine[cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:LineExtensionAmount), '###.##0,00', 'european')"/>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL'">
                                                        <xsl:text>TL</xsl:text>
                                                    </xsl:if>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL'">
                                                        <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                                                    </xsl:if>
                                                </td>
                                            </tr>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="211px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Tevkifata Tabi İşlem Üzerinden Hes. KDV</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:value-of
                                                            select="format-number(sum(n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:TaxableAmount), '###.##0,00', 'european')"/>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL'">
                                                        <xsl:text>TL</xsl:text>
                                                    </xsl:if>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL'">
                                                        <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                                                    </xsl:if>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="211px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Tevkifata Tabi İşlem Tutarı</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:if test="n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]">
                                                        <xsl:value-of
                                                                select="format-number(sum(n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]/cbc:LineExtensionAmount), '###.##0,00', 'european')"
                                                        />
                                                    </xsl:if>
                                                    <xsl:if test="//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=&apos;9015&apos;">
                                                        <xsl:value-of
                                                                select="format-number(sum(n1:Invoice/cac:InvoiceLine[cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:LineExtensionAmount), '###.##0,00', 'european')"
                                                        />
                                                    </xsl:if>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL' or n1:Invoice/cbc:DocumentCurrencyCode = 'TRY'">
                                                        <xsl:text>TL</xsl:text>
                                                    </xsl:if>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL' and n1:Invoice/cbc:DocumentCurrencyCode != 'TRY'">
                                                        <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                                                    </xsl:if>
                                                </td>
                                            </tr>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="211px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Tevkifata Tabi İşlem Üzerinden Hes. KDV</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:if test="n1:Invoice/cac:InvoiceLine[cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme]">
                                                        <xsl:value-of
                                                                select="format-number(sum(n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme]/cbc:TaxableAmount), '###.##0,00', 'european')"
                                                        />
                                                    </xsl:if>
                                                    <xsl:if test="//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=&apos;9015&apos;">
                                                        <xsl:value-of
                                                                select="format-number(sum(n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode=9015]/cbc:TaxableAmount), '###.##0,00', 'european')"
                                                        />
                                                    </xsl:if>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode = 'TRL' or n1:Invoice/cbc:DocumentCurrencyCode = 'TRY'">
                                                        <xsl:text>TL</xsl:text>
                                                    </xsl:if>
                                                    <xsl:if test="n1:Invoice/cbc:DocumentCurrencyCode != 'TRL' and n1:Invoice/cbc:DocumentCurrencyCode != 'TRY'">
                                                        <xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>
                                                    </xsl:if>
                                                </td>
                                            </tr>
                                        </xsl:if>

                                        <xsl:if test="not($varfaturatipi='SGK' and $varoptik='medula') and  $varExportCarriage &gt; 0">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Navlun</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:value-of
                                                            select="format-number(sum(//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:DeclaredForCarriageValueAmount), '###.##0,00', 'european')"/>
                                                    <xsl:if
                                                            test="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:DeclaredForCarriageValueAmount/@currencyID">
                                                        <xsl:text> </xsl:text>
                                                        <xsl:if
                                                                test="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:DeclaredForCarriageValueAmount/@currencyID = 'TRL' or //n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:DeclaredForCarriageValueAmount/@currencyID = 'TRY'">
                                                            <xsl:text>TL</xsl:text>
                                                        </xsl:if>
                                                        <xsl:if
                                                                test="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:DeclaredForCarriageValueAmount/@currencyID != 'TRL' and //n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:DeclaredForCarriageValueAmount/@currencyID != 'TRY'">
                                                            <xsl:value-of
                                                                    select="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:DeclaredForCarriageValueAmount/@currencyID"
                                                            />
                                                        </xsl:if>
                                                    </xsl:if>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="not($varfaturatipi='SGK' and $varoptik='medula') and  $varExportInsurance &gt; 0">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Sigorta</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:value-of
                                                            select="format-number(sum(//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:InsuranceValueAmount), '###.##0,00', 'european')"/>
                                                    <xsl:if
                                                            test="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:InsuranceValueAmount/@currencyID">
                                                        <xsl:text> </xsl:text>
                                                        <xsl:if
                                                                test="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:InsuranceValueAmount/@currencyID = 'TRL' or //n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:InsuranceValueAmount/@currencyID = 'TRY'">
                                                            <xsl:text>TL</xsl:text>
                                                        </xsl:if>
                                                        <xsl:if
                                                                test="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:InsuranceValueAmount/@currencyID != 'TRL' and //n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:InsuranceValueAmount/@currencyID != 'TRY'">
                                                            <xsl:value-of
                                                                    select="//n1:Invoice/cac:InvoiceLine/cac:Delivery/cac:Shipment/cbc:InsuranceValueAmount/@currencyID"
                                                            />
                                                        </xsl:if>
                                                    </xsl:if>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="not($varfaturatipi='SGK' and $varoptik='medula')">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Vergiler Dahil Toplam Tutar</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:for-each
                                                            select="n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount">
                                                        <xsl:call-template name="Curr_Type"/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                        </xsl:if>

                                        <xsl:if test="(//n1:Invoice/cbc:ProfileID='HKS' and //n1:Invoice/cbc:InvoiceTypeCode='KOMISYONCU') or (//n1:Invoice/cbc:ProfileID='EARSIVFATURA' and //n1:Invoice/cbc:InvoiceTypeCode='HKSKOMISYONCU')">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Toplam Masraflar</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:ChargeTotalAmount">
                                                        <xsl:call-template name="Curr_Type"/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="not($varfaturatipi='SGK') ">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Ödenecek Tutar</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount">
                                                        <xsl:call-template name="Curr_Type"/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:for-each select="n1:Invoice/cac:Delivery/cac:Shipment/cbc:DeclaredCustomsValueAmount">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Toplam Byn. Edl. Kıymet Değeri</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:call-template name="Curr_Type"/>
                                                </td>
                                            </tr>
                                        </xsl:for-each>
                                        <xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
                                            <xsl:if test="//n1:Invoice/cbc:DocumentCurrencyCode != 'TRY' and //n1:Invoice/cbc:DocumentCurrencyCode != 'TRL'">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" align="right" width="200px">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Hesaplanan </xsl:text>
                                                            <xsl:value-of select="cac:TaxCategory/cac:TaxScheme/cbc:Name"/>
                                                            <xsl:text>(%</xsl:text>
                                                            <xsl:value-of select="cbc:Percent"/>
                                                            <xsl:text>) (TL)</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                        <span>
                                                            <xsl:value-of
                                                                    select="format-number(cbc:TaxAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
                                                            <xsl:text> TL</xsl:text>
                                                        </span>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                        </xsl:for-each>
                                        <xsl:if test="($varfaturatipi='SGK' and not($varoptik='medula'))">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Toplam Ödenecek Tutar</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount">
                                                        <xsl:call-template name="Curr_Type"/>
                                                    </xsl:for-each>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="($varfaturatipi='SGK' and $varoptik='medula')">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>KDV Hariç Ödenecek Tutar</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <span>
                                                        <xsl:value-of select="format-number(number(n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount) - (number($varkdv8) + number($varkdv10) + number($varkdv18) + number($varkdv20) + number($vareczanekdv18)+ number($vareczanekdv20)), '###.##0,00', 'european')" />
                                                        <xsl:text> TL</xsl:text>
                                                    </span>
                                                </td>
                                            </tr>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>KDV(%8 + %10 + %18 + %20)</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <span>
                                                        <xsl:value-of select="format-number((number($varkdv8) + number($varkdv10) + number($varkdv18) + number($varkdv20) + number($vareczanekdv18) + number($vareczanekdv20)), '###.##0,00', 'european')" />
                                                        <xsl:text> TL</xsl:text>
                                                    </span>
                                                </td>
                                            </tr>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Toplam Ödenecek Tutar</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount">
                                                            <xsl:call-template name="Curr_Type"/>
                                                        </xsl:for-each>
                                                    </span>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                        <xsl:if test="//n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount/@currencyID != 'TRL' and //n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount/@currencyID != 'TRY'">
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" align="right" width="200px">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Mal Hizmet Toplam Tutarı(TL)</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:81px; " align="right">
                                                    <xsl:value-of
                                                            select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
                                                    <xsl:text> TL</xsl:text>
                                                </td>
                                            </tr>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Vergiler Dahil Toplam Tutar(TL)</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:value-of
                                                            select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
                                                    <xsl:text> TL</xsl:text>
                                                </td>
                                            </tr>
                                            <xsl:if test="not($varfaturatipi='SGK')">
                                                <tr align="right">
                                                    <td/>
                                                    <td class="lineTableBudgetTd" width="200px" align="right">
                                                        <span style="font-weight:bold; ">
                                                            <xsl:text>Vergiler Dahil Toplam Tutar</xsl:text>
                                                        </span>
                                                    </td>
                                                    <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                        <xsl:for-each select="n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount">
                                                            <xsl:call-template name="Curr_Type"/>
                                                        </xsl:for-each>
                                                    </td>
                                                </tr>
                                            </xsl:if>
                                            <tr align="right">
                                                <td/>
                                                <td class="lineTableBudgetTd" width="200px" align="right">
                                                    <span style="font-weight:bold; ">
                                                        <xsl:text>Ödenecek Tutar(TL)</xsl:text>
                                                    </span>
                                                </td>
                                                <td class="lineTableBudgetTd" style="width:82px; " align="right">
                                                    <xsl:value-of
                                                            select="format-number(//n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount * //n1:Invoice/cac:PricingExchangeRate/cbc:CalculationRate, '###.##0,00', 'european')"/>
                                                    <xsl:text> TL</xsl:text>
                                                </td>
                                            </tr>
                                        </xsl:if>
                                    </tbody>
                                </table>
                            </td>
                        </tr>
                    </tbody>
                </table>
                <br/>
                <xsl:if test="//n1:Invoice/cac:BillingReference/cac:InvoiceDocumentReference/cbc:DocumentTypeCode[text()='İADE' or text()='IADE']">
                    <table id="lineTable" width="800">
                        <thead>
                            <tr>
                                <td align="left">
                                    <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;İadeye Konu Olan Faturalar</span>
                                </td>
                            </tr>
                        </thead>
                        <tbody>
                            <tr align="left" class="lineTableTr">
                                <td class="lineTableTd">
                                    <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;Fatura No</span>
                                </td>
                                <td class="lineTableTd">
                                    <span style="font-weight:bold; " align="center">&#160;&#160;&#160;&#160;&#160;Tarih</span>
                                </td>
                            </tr>
                            <xsl:for-each select="//n1:Invoice/cac:BillingReference/cac:InvoiceDocumentReference/cbc:DocumentTypeCode[text()='İADE' or text()='IADE']">
                                <tr align="left" class="lineTableTr">
                                    <td class="lineTableTd">&#160;&#160;&#160;&#160;&#160;
                                        <xsl:value-of select="../cbc:ID"/>
                                    </td>
                                    <td class="lineTableTd">&#160;&#160;&#160;&#160;&#160;
                                        <xsl:for-each select="../cbc:IssueDate">
                                            <xsl:apply-templates select="."/>
                                        </xsl:for-each>
                                    </td>
                                </tr>
                            </xsl:for-each>
                        </tbody>
                    </table>
                </xsl:if>
                <br/>
                <xsl:if test="//n1:Invoice/cac:BillingReference/cac:AdditionalDocumentReference/cbc:DocumentTypeCode='OKCBF'">
                    <table border="1" id="lineTable" width="800">
                        <thead>
                            <tr>
                                <th colspan="6">ÖKC Bilgileri</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr id="okcbfHeadTr" style="font-weight:bold;">
                                <td style="width:20%">
                                    <xsl:text>Fiş Numarası</xsl:text>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:text>Fiş Tarihi</xsl:text>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:text>Fiş Saati</xsl:text>
                                </td>
                                <td style="width:40%" align="center">
                                    <xsl:text>Fiş Tipi</xsl:text>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:text>Z Rapor No</xsl:text>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:text>ÖKC Seri No</xsl:text>
                                </td>
                            </tr>
                        </tbody>
                        <xsl:for-each select="//n1:Invoice/cac:BillingReference/cac:AdditionalDocumentReference/cbc:DocumentTypeCode[text()='OKCBF']">
                            <tr>
                                <td style="width:20%">
                                    <xsl:value-of select="../cbc:ID"/>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:value-of select="../cbc:IssueDate"/>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:value-of select="substring(../cac:ValidityPeriod/cbc:StartTime,1,5)"/>
                                </td>
                                <td style="width:40%" align="center">
                                    <xsl:choose>
                                        <xsl:when test="../cbc:DocumentDescription='AVANS'">
                                            <xsl:text>Ön Tahsilat(Avans) Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='YEMEK_FIS'">
                                            <xsl:text>Yemek Fişi/Kartı ile Yapılan Tahsilat Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='E-FATURA'">
                                            <xsl:text>E-Fatura Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='E-FATURA_IRSALIYE'">
                                            <xsl:text>İrsaliye Yerine Geçen E-Fatura Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='E-ARSIV'">
                                            <xsl:text>E-Arşiv Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='E-ARSIV_IRSALIYE'">
                                            <xsl:text>İrsaliye Yerine Geçen E-Arşiv Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='FATURA'">
                                            <xsl:text>Faturalı Satış Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='OTOPARK'">
                                            <xsl:text>Otopark Giriş Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='FATURA_TAHSILAT'">
                                            <xsl:text>Fatura Tahsilat Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:when test="../cbc:DocumentDescription='FATURA_TAHSILAT_KOMISYONLU'">
                                            <xsl:text>Komisyonlu Fatura Tahsilat Bilgi Fişi</xsl:text>
                                        </xsl:when>
                                        <xsl:otherwise>
                                            <xsl:text> </xsl:text>
                                        </xsl:otherwise>
                                    </xsl:choose>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:value-of select="../cac:Attachment/cac:ExternalReference/cbc:URI"/>
                                </td>
                                <td style="width:10%" align="center">
                                    <xsl:value-of select="../cac:IssuerParty/cbc:EndpointID"/>
                                </td>
                            </tr>
                        </xsl:for-each>
                    </table>
                    <br/>
                </xsl:if>


                <table id="notesTable" style="width: 800px;">
                    <tbody>
                        <xsl:if test="($varfaturatipi='SGK' and $varoptik='medula')">
                            <tr>
                                <td style="width: 100%;">
                                    <table style="margin-top: 10px; margin-left: 10px;">
                                        <tbody>
                                            <tr>
                                                <td style="width: 25%; vertical-align: top;">
                                                    <table style="width: 100%;">
                                                        <tbody>
                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($varmaasdanilackatilimpayi='null' or $varmaasdanilackatilimpayi='' or $vareldenmuayenekatilimpayi='' or $vareldenrecetekatilimpayi='' or $varfaturatype='CETAS') or $varmaasdanilackatilimpayi='null'">
                                                                <tr>
                                                                    <td style="width: 50%; text-decoration: underline; font-weight: bold;">
                                                                        <xsl:text>Elden Tahsil Edilen</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>

                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($varmaasdanilackatilimpayi='null' or $varmaasdanilackatilimpayi='' or $varfaturatype='CETAS')">
                                                                <tr style="line-height: 14px;">
                                                                    <td style="width: 50%;">
                                                                        <xsl:text>İlaç Kat. Payı(Elden):</xsl:text>
                                                                    </td>

                                                                    <td style="width: 50%;">
                                                                        <xsl:value-of select="format-number(number($vareldenilackatilimpayi), '###.##0,00', 'european')" />
                                                                        <xsl:text> TL</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>

                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($vareldenmuayenekatilimpayi='' or $varfaturatype='CETAS')">
                                                                <tr style="line-height: 14px;">
                                                                    <td style="width: 50%;">
                                                                        <xsl:text>Muayene Kat. Payı(Elden):</xsl:text>
                                                                    </td>

                                                                    <td style="width: 50%;">
                                                                        <xsl:value-of select="format-number(number($vareldenmuayenekatilimpayi), '###.##0,00', 'european')"/>
                                                                        <xsl:text> TL</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>

                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($vareldenrecetekatilimpayi='' or $varfaturatype='CETAS')">
                                                                <tr style="line-height: 14px;">
                                                                    <td style="width: 50%;">
                                                                        <xsl:text>Reçete Kat. Payı(Elden):</xsl:text>
                                                                    </td>

                                                                    <td style="width: 50%;">
                                                                        <xsl:value-of select="format-number(number($vareldenrecetekatilimpayi), '###.##0,00', 'european')"/>
                                                                        <xsl:text> TL</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>

                                                            <xsl:if test="$varmaasdanilackatilimpayi='null'">
                                                                <tr style="line-height: 14px;">
                                                                    <td style="width: 60%;">
                                                                        <xsl:text>İlaç Kat. Payı(Elden + Maaş):</xsl:text>
                                                                    </td>

                                                                    <td style="width: 40%;">
                                                                        <xsl:value-of select="format-number(number($vareldenilackatilimpayi), '###.##0,00', 'european')" />
                                                                        <xsl:text> TL</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>


                                                        </tbody>
                                                    </table>
                                                </td>

                                                <td style="width: 25%; vertical-align: top;">
                                                    <table style="width: 100%">
                                                        <tbody>
                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($varmaasdanilackatilimpayi='null' or $varmaasdanilackatilimpayi='' or $varmaasmuayenekatilimpayi='' or $varmaastanrecetekatilimpayi='' or $varfaturatype='CETAS')">
                                                                <tr>
                                                                    <td style="width: 50%; text-decoration: underline; font-weight: bold;">
                                                                        <xsl:text>Maaştan Kesilen</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>

                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($varmaasdanilackatilimpayi='null' or $varmaasdanilackatilimpayi='' or $varfaturatype='CETAS')">
                                                                <tr style="line-height: 14px;">
                                                                    <td style="width: 50%;">
                                                                        <xsl:text>İlaç Kat. Payı(Maaş)</xsl:text>
                                                                    </td>

                                                                    <td style="width: 50%;">
                                                                        <xsl:value-of select="format-number(number($varmaasdanilackatilimpayi), '###.##0,00', 'european')" />
                                                                        <xsl:text> TL</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>

                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($varmaasmuayenekatilimpayi='' or $varfaturatype='CETAS')">
                                                                <tr style="line-height: 14px;">
                                                                    <td style="width: 50%;">
                                                                        <xsl:text>Muayene Kat. Payı(Maaş)</xsl:text>
                                                                    </td>

                                                                    <td style="width: 50%;">
                                                                        <xsl:value-of select="format-number(number($varmaasmuayenekatilimpayi), '###.##0,00', 'european')"/>
                                                                        <xsl:text> TL</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>

                                                            <!--TEBEOS Cezaevi faturası için sgk şeklinde bir tasarım istermiştir ona göre düzenleme yapılmıştır.
                                                                Resources altına oluşturdukları xml atılacaktır. Değişiklik yaparken o xmle de görede kontrol sağlanmalıdır.-->
                                                            <xsl:if test="not($varmaastanrecetekatilimpayi='' or $varfaturatype='CETAS')">
                                                                <tr style="line-height: 14px;">
                                                                    <td style="width: 50%;">
                                                                        <xsl:text>Reçete Kat. Payı(Maaş):</xsl:text>
                                                                    </td>

                                                                    <td style="width: 50%;">
                                                                        <xsl:value-of select="format-number(number($varmaastanrecetekatilimpayi), '###.##0,00', 'european')"/>
                                                                        <xsl:text> TL</xsl:text>
                                                                    </td>
                                                                </tr>
                                                            </xsl:if>
                                                        </tbody>
                                                    </table>
                                                </td>
                                            </tr>
                                        </tbody>
                                    </table>
                                </td>
                            </tr>
                        </xsl:if>
                        <tr align="left">
                            <td id="notesTableTd" style="width: 80%" height="100">
                                <xsl:for-each select="//n1:Invoice/cac:TaxTotal/cac:TaxSubtotal">
                                    <xsl:if test="(cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015' or ../../cbc:InvoiceTypeCode='OZELMATRAH') and cac:TaxCategory/cbc:TaxExemptionReason">
                                        <b>&#160;&#160;&#160;&#160;&#160; Vergi İstisna Muafiyet Sebebi: </b>
                                        <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReasonCode"/>
                                        <xsl:text>-</xsl:text>
                                        <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReason"/>
                                        <br/>
                                    </xsl:if>
                                    <xsl:if test="starts-with(cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode,'007') and cac:TaxCategory/cbc:TaxExemptionReason">
                                        <b>&#160;&#160;&#160;&#160;&#160; ÖTV İstisna Muafiyet Sebebi: </b>
                                        <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReasonCode"/>
                                        <xsl:text>-</xsl:text>
                                        <xsl:value-of select="cac:TaxCategory/cbc:TaxExemptionReason"/>
                                        <br/>
                                    </xsl:if>
                                </xsl:for-each>
                                <xsl:for-each select="//n1:Invoice/cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
                                    <b>&#160;&#160;&#160;&#160;&#160; Tevkifat Sebebi: </b>
                                    <xsl:value-of select="cbc:TaxTypeCode"/>
                                    <xsl:text>-</xsl:text>
                                    <xsl:value-of select="cbc:Name"/>
                                    <br/>
                                </xsl:for-each>

                                <!-- NOTE_REF -->

                                <xsl:for-each select="//n1:Invoice/cbc:Note">
                                    <xsl:if test="not(starts-with(.,'SGK_')) and not(starts-with(.,'FATURATIPI:SGK')) and not(starts-with(.,'SS:')) and not(starts-with(.,'FATURA_TYPE:CETAS'))">
                                        <b>&#160;&#160;&#160;&#160;&#160; Not: </b>
                                        <xsl:value-of select="."/>
                                        <br/>
                                    </xsl:if>
                                </xsl:for-each>
                                <xsl:for-each select="//n1:Invoice/cac:AdditionalDocumentReference">
                                    <xsl:if test="cbc:ID='INTERNET_SATIS'">
                                        <b>&#160;&#160;&#160;&#160;&#160; Not: </b>
                                        <xsl:text>Bu satış internet üzerinden yapılmıştır.</xsl:text>
                                        <br/>

                                        <b>&#160;&#160;&#160;&#160;&#160; Ödeme Şekli: </b>
                                        <xsl:value-of select="cbc:DocumentType"/>
                                        <br/>

                                        <b>&#160;&#160;&#160;&#160;&#160; Satışın Yapıldığı Web Adresi: </b>
                                        <xsl:value-of select="cac:IssuerParty/cbc:WebsiteURI"/>
                                        <br/>
                                        <b>&#160;&#160;&#160;&#160;&#160; Ödeme Tarihi: </b>
                                        <xsl:value-of select="cbc:IssueDate"/>
                                        <br/>
                                    </xsl:if>
                                </xsl:for-each>
                                <xsl:if test="//n1:Invoice/cac:Delivery">
                                    <xsl:if test="//n1:Invoice/cac:Delivery/cac:CarrierParty/cac:PartyIdentification/cbc:ID[@schemeID='VKN']">
                                        <b>&#160;&#160;&#160;&#160;&#160; Taşıyıcı Unvan: </b>
                                        <xsl:value-of select="//n1:Invoice/cac:Delivery/cac:CarrierParty/cac:PartyName/cbc:Name"/>
                                        <br/>
                                    </xsl:if>
                                    <xsl:if test="//n1:Invoice/cac:Delivery/cac:CarrierParty/cac:PartyIdentification/cbc:ID[@schemeID='TCKN']">
                                        <b>&#160;&#160;&#160;&#160;&#160; Taşıyıcı Ad-Soyad: </b>
                                        <xsl:value-of select="//n1:Invoice/cac:Delivery/cac:CarrierParty/cac:Person/cbc:FirstName"/>
                                        <xsl:text> </xsl:text>
                                        <xsl:value-of select="//n1:Invoice/cac:Delivery/cac:CarrierParty/cac:Person/cbc:FamilyName"/>
                                        <br/>
                                    </xsl:if>
                                    <b>&#160;&#160;&#160;&#160;&#160; Taşıyıcı TCKN/VKN: </b>
                                    <xsl:value-of select="//n1:Invoice/cac:Delivery/cac:CarrierParty/cac:PartyIdentification/cbc:ID"/>
                                    <br/>
                                    <b>&#160;&#160;&#160;&#160;&#160; Gönderim Tarihi: </b>
                                    <xsl:value-of select="//n1:Invoice/cac:Delivery/cbc:ActualDeliveryDate"/>
                                    <br/>
                                </xsl:if>
                                <xsl:if test="//n1:Invoice/cac:PaymentMeans/cbc:InstructionNote">
                                    <b>&#160;&#160;&#160;&#160;&#160; Ödeme Notu: </b>
                                    <xsl:value-of
                                            select="//n1:Invoice/cac:PaymentMeans/cbc:InstructionNote"/>
                                    <br/>
                                </xsl:if>
                                <xsl:if
                                        test="//n1:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:PaymentNote">
                                    <b>&#160;&#160;&#160;&#160;&#160; Hesap Açıklaması: </b>
                                    <xsl:value-of
                                            select="//n1:Invoice/cac:PaymentMeans/cac:PayeeFinancialAccount/cbc:PaymentNote"/>
                                    <br/>
                                </xsl:if>
                                <xsl:if test="//n1:Invoice/cac:PaymentTerms/cbc:Note">
                                    <b>&#160;&#160;&#160;&#160;&#160; Ödeme Koşulu: </b>
                                    <xsl:value-of select="//n1:Invoice/cac:PaymentTerms/cbc:Note"/>
                                    <br/>
                                </xsl:if>
                                <xsl:if test="//n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='PARTYTYPE']='TAXFREE' and //n1:Invoice/cac:TaxRepresentativeParty/cac:PartyTaxScheme/cbc:ExemptionReasonCode">
                                    <br/>
                                    <b>&#160;&#160;&#160;&#160;&#160; VAT OFF - NO CASH REFUND </b>
                                </xsl:if>
                            </td>
                            <td style="width: 20%">

                            </td>
                        </tr>
                    </tbody>
                </table>
                <!-- ORDER-REFUND -->
               <!-- <table border="1" cellpadding="1" cellspacing="1" style="width: 800px;"> 
                    <tbody><tr>
                    
                </tr></tbody> 
                </table> -->

                <div id="qrvalue" style="visibility: hidden; height: 20px;width: 20px; ; display:none">
{"vkntckn":"<xsl:value-of select="n1:Invoice/cac:AccountingSupplierParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='TCKN' or @schemeID='VKN']"/>",
"avkntckn":"<xsl:value-of select="n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='TCKN' or @schemeID='VKN']"/>",
<xsl:if test="//n1:Invoice/cbc:ProfileID = 'YOLCUBERABERFATURA'">"pasaportno":"<xsl:value-of select="n1:Invoice/cac:BuyerCustomerParty/cac:Party/cac:Person/cac:IdentityDocumentReference/cbc:ID"/>",</xsl:if>
<xsl:if test="//n1:Invoice/cbc:ProfileID = 'YOLCUBERABERFATURA'">"aracikurumvkn":"<xsl:value-of select="n1:Invoice/cac:TaxRepresentativeParty/cac:PartyIdentification/cbc:ID[@schemeID='ARACIKURUMVKN']"/>",</xsl:if>
"senaryo":"<xsl:value-of select="n1:Invoice/cbc:ProfileID"/>",
"tip":"<xsl:value-of select="n1:Invoice/cbc:InvoiceTypeCode"/>",
"tarih":"<xsl:value-of select="n1:Invoice/cbc:IssueDate"/>",
"no":"<xsl:value-of select="n1:Invoice/cbc:ID"/>",
"ettn":"<xsl:value-of select="n1:Invoice/cbc:UUID"/>",
"parabirimi":"<xsl:value-of select="n1:Invoice/cbc:DocumentCurrencyCode"/>",
"malhizmettoplam":"<xsl:value-of select="n1:Invoice/cac:LegalMonetaryTotal/cbc:LineExtensionAmount"/>",<xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015']">
"kdvmatrah(<xsl:value-of select="cbc:Percent"/>)":"<xsl:value-of select="cbc:TaxableAmount"/>",</xsl:for-each><xsl:for-each select="n1:Invoice/cac:TaxTotal/cac:TaxSubtotal[cac:TaxCategory/cac:TaxScheme/cbc:TaxTypeCode='0015']">
"hesaplanankdv(<xsl:value-of select="cbc:Percent"/>)":"<xsl:value-of select="cbc:TaxAmount"/>",</xsl:for-each>
"vergidahil":"<xsl:value-of select="n1:Invoice/cac:LegalMonetaryTotal/cbc:TaxInclusiveAmount"/>",
"odenecek":"<xsl:value-of select="n1:Invoice/cac:LegalMonetaryTotal/cbc:PayableAmount"/>"}
                </div>
                <script type="text/javascript">
                    var qrcode = new QRCode(document.getElementById("qrcode"), {
                    width : 170,
                    height : 170,
                    correctLevel : QRCode.CorrectLevel.L
                    });

                    function makeCode (msg) {
                    qrcode.makeCode(msg);
                    }

                    makeCode(document.getElementById("qrvalue").innerHTML.replace(/\s/g, ''));
                </script>
            <!-- DEV-MODE-OVERRIDES:START -->
<script>(function(){
  function jc(a){return a==='center'?'center':a==='right'?'flex-end':'flex-start';}
  function setImg(ids,url,w,h,align){
    ids.forEach(function(id){
      var el=document.getElementById(id);if(!el)return;
      el.style.cssText='display:flex;justify-content:'+jc(align)+';align-items:center;';
      var img=document.createElement('img');
      img.src=url;
      img.style.cssText=(w?'width:'+w+'px;':'width:auto;')+(h?'height:'+h+'px;':'height:auto;')+'max-width:100%;object-fit:contain;display:block;';
      el.innerHTML='';el.appendChild(img);
    });
  }
  window.addEventListener('load',function(){
    setImg(['company_logo','companyLogo'],'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',200,120,'right');
    setImg(['imza','companyKase'],'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',150,100,'center');
  });
})();</script>
<!-- DEV-MODE-OVERRIDES:END -->
</body>
        </html>
    </xsl:template>
    <xsl:template match="//n1:Invoice/cac:InvoiceLine">
        <tr class="lineTableTr">
            <td class="lineTableTd">
                <xsl:text>&#160;</xsl:text>
                <xsl:value-of select="./cbc:ID"/>
            </td>
            <xsl:choose>
                <xsl:when test="$varItemCode &gt; 0">
                    <td class="lineTableTd">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:value-of select="./cac:Item/cac:SellersItemIdentification/cbc:ID"/>
                    </td>
                </xsl:when>
                <xsl:otherwise>
                </xsl:otherwise>
            </xsl:choose>
            <xsl:choose>
                <xsl:when test="//n1:Invoice/cac:AccountingCustomerParty/cac:Party/cac:PartyIdentification/cbc:ID[@schemeID='VKN' and text()='7350019759']">
                    <td class="lineTableTd">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:for-each select="./cbc:Note">
                            <xsl:if test="contains(.,'#SS_Satir_NO=')">
                                <xsl:value-of select="normalize-space(substring-after(substring(.,12),'='))"/>
                            </xsl:if>
                        </xsl:for-each>
                    </td>
                </xsl:when>
            </xsl:choose>
            <td class="lineTableTd">
                <xsl:text>&#160;</xsl:text>
                <xsl:value-of select="./cac:Item/cbc:Name"/>
            </td>

            <xsl:if test="not($varfaturatipi='SGK')">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:value-of select="format-number(./cbc:InvoicedQuantity, '###.##0,####', 'european')"/>
                    <xsl:if test="./cbc:InvoicedQuantity/@unitCode">
                        <xsl:for-each select="./cbc:InvoicedQuantity">
                            <xsl:text> </xsl:text>
                            <xsl:choose>
                                <xsl:when test="@unitCode = 'BX'">
                                    <xsl:text>Kutu</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'TNE'">
                                    <xsl:text>ton</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'BX'">
                                    <xsl:text>Kutu</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'LTR'">
                                    <xsl:text>lt</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'C62'">
                                    <xsl:text>Adet</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'TN'">
                                    <xsl:text>Teneke</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KGM'">
                                    <xsl:text>kg</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KJO'">
                                    <xsl:text>kJ</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'GRM'">
                                    <xsl:text>g</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MGM'">
                                    <xsl:text>mg</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'NT'">
                                    <xsl:text>Net Ton</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'GT'">
                                    <xsl:text>Gross Ton</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MTR'">
                                    <xsl:text>m</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MMT'">
                                    <xsl:text>mm</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KTM'">
                                    <xsl:text>km</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MLT'">
                                    <xsl:text>ml</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MMQ'">
                                    <xsl:text>mm3</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CLT'">
                                    <xsl:text>cl</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CMK'">
                                    <xsl:text>cm2</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CMQ'">
                                    <xsl:text>cm3</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CMT'">
                                    <xsl:text>cm</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MTK'">
                                    <xsl:text>m2</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MTQ'">
                                    <xsl:text>m3</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'DAY'">
                                    <xsl:text>Gün</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MON'">
                                    <xsl:text>Ay</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'PA'">
                                    <xsl:text>Paket</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KWH'">
                                    <xsl:text>KWH</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'ANN'">
                                    <xsl:text>Yıl</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'HUR'">
                                    <xsl:text>Saat</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'D61'">
                                    <xsl:text>Dakika</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'D62'">
                                    <xsl:text>Saniye</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CCT'">
                                    <xsl:text>Ton baş.taşıma kap.</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'D30'">
                                    <xsl:text>Brüt kalori</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'D40'">
                                    <xsl:text>1000 lt</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'LPA'">
                                    <xsl:text>saf alkol lt</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'B32'">
                                    <xsl:text>kg.m2</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'NCL'">
                                    <xsl:text>hücre adet</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'PR'">
                                    <xsl:text>Çift</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'R9'">
                                    <xsl:text>1000 m3</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'SET'">
                                    <xsl:text>Set</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'T3'">
                                    <xsl:text>1000 adet</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'PK'">
                                    <xsl:text>Koli</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CR'">
                                    <xsl:text>Kasa/Sandık</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'BG'">
                                    <xsl:text>Poşet/Torba</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'GFI'">
                                    <xsl:text>Fıssıle İzotop Gramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CEN'">
                                    <xsl:text>Yüz Adet</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KPO'">
                                    <xsl:text>Kilogram Potasyum Oksit</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MND'">
                                    <xsl:text>Kurutulmuş Net Ağırlıklı Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = '3I'">
                                    <xsl:text>Kilogram-Adet</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KFO'">
                                    <xsl:text>Difosfor Pentaoksit Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KHY'">
                                    <xsl:text>Hidrojen Peroksik Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KMA'">
                                    <xsl:text>Metil Aminlerin Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KNI'">
                                    <xsl:text>Azotun Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KPH'">
                                    <xsl:text>Kilogram Potasyum Hidroksit</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KSD'">
                                    <xsl:text>%90 Kuru Ürün Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KSH'">
                                    <xsl:text>Sodyum Hidroksit Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KUR'">
                                    <xsl:text>Uranyum Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'D32'">
                                    <xsl:text>Terawatt Saat</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'GWH'">
                                    <xsl:text>Gigawatt Saat</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'MWH'">
                                    <xsl:text>Megawatt Saat (1000 kW.h)</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KWT'">
                                    <xsl:text>Kilowatt</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'DMK'">
                                    <xsl:text>Desimetre Kare</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CTM'">
                                    <xsl:text>Karat</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'SM3'">
                                    <xsl:text>Standart Metreküp</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CT'">
                                    <xsl:text>Karton</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'DMT'">
                                    <xsl:text>Desimetre</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'DMQ'">
                                    <xsl:text>Desimetre Küp</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KTN'">
                                    <xsl:text>Kiloton</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'D93'">
                                    <xsl:text>Doz</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'LM'">
                                    <xsl:text>Metre Tül</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'BO'">
                                    <xsl:text>Şişe</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'H80'">
                                    <xsl:text>Rack Unit</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'RA'">
                                    <xsl:text>Rack</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'TU'">
                                    <xsl:text>Tüp</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'BLL'">
                                    <xsl:text>Fıçı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'TC'">
                                    <xsl:text>Kamyon</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'PG'">
                                    <xsl:text>Plaka</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'DPC'">
                                    <xsl:text>Düzüne</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'LR'">
                                    <xsl:text>Tabaka</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'JOU'">
                                    <xsl:text>Vardiya</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'DRL'">
                                    <xsl:text>Rulo</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'ACR'">
                                    <xsl:text>Dönüm</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'E53'">
                                    <xsl:text>Test</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'H82'">
                                    <xsl:text>Puan</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'SQR'">
                                    <xsl:text>Ayak</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'AYR'">
                                    <xsl:text>Altın Ayarı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'BAS'">
                                    <xsl:text>Bas</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'CPR'">
                                    <xsl:text>Adet-Çift</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'GMS'">
                                    <xsl:text>Gümüş</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'H62'">
                                    <xsl:text>Yüz Adet</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KHO'">
                                    <xsl:text>Hidroje Peroksit Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KH6'">
                                    <xsl:text>Kilogram-Baş</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KOH'">
                                    <xsl:text>Kilogram Potasyum Hidroksit</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'KPR'">
                                    <xsl:text>Kilogram-Çift</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'K20'">
                                    <xsl:text>Kilogram Potasyum Oksit</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'K58'">
                                    <xsl:text>Kurutulmuş Net Ağırlıklı Kilogramı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'K62'">
                                    <xsl:text>Kilogram-Adet</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'NCR'">
                                    <xsl:text>Karat</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'OMV'">
                                    <xsl:text>OTV Maktu Vergi</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'OTB'">
                                    <xsl:text>OTV Birim Fiyatı</xsl:text>
                                </xsl:when>
                                <xsl:when test="@unitCode = 'D63'">
                                    <xsl:text>Cilt</xsl:text>
                                </xsl:when>
                            </xsl:choose>
                        </xsl:for-each>
                    </xsl:if>
                </td>
                <xsl:if test="$varEtiketFiyati='1'">
                    <td class="lineTableTd" align="right">
                        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine/cbc:Note">
                            <xsl:if test="contains(.,'ETF:') or contains(.,'ESF:')">
                                <xsl:value-of select="substring-after(substring(.,4),':')" />
                                <xsl:text> </xsl:text>
                                <xsl:if test="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRL&quot; or //n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRY&quot;">
                                    <xsl:text>TL</xsl:text>
                                </xsl:if>
                                <xsl:if test="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRL&quot; and //n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRY&quot;">
                                    <xsl:value-of select="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID"/>
                                </xsl:if>
                            </xsl:if>
                        </xsl:for-each>
                    </td>
                </xsl:if>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:value-of
                            select="format-number(./cac:Price/cbc:PriceAmount, '###.##0,########', 'european')"/>
                    <xsl:if test="./cac:Price/cbc:PriceAmount/@currencyID">
                        <xsl:text> </xsl:text>
                        <xsl:if test="./cac:Price/cbc:PriceAmount/@currencyID = &quot;TRL&quot; or ./cac:Price/cbc:PriceAmount/@currencyID = &quot;TRY&quot;">
                            <xsl:text>TL</xsl:text>
                        </xsl:if>
                        <xsl:if test="./cac:Price/cbc:PriceAmount/@currencyID != &quot;TRL&quot; and ./cac:Price/cbc:PriceAmount/@currencyID != &quot;TRY&quot;">
                            <xsl:value-of select="./cac:Price/cbc:PriceAmount/@currencyID"/>
                        </xsl:if>
                    </xsl:if>
                </td>
                <xsl:if test="$varDepocuFiyati='1'">
                    <td class="lineTableTd" align="right">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine/cbc:Note">
                            <xsl:if test="contains(.,'DSF:')">
                                <br/>
                                <xsl:value-of select="substring-after(substring(.,4),':')" />
                                <xsl:text> </xsl:text>
                                <xsl:if test="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRL&quot; or //n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRY&quot;">
                                    <xsl:text>TL</xsl:text>
                                </xsl:if>
                                <xsl:if test="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRL&quot; and .//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRY&quot;">
                                    <xsl:value-of select="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID"/>
                                </xsl:if>
                            </xsl:if>
                        </xsl:for-each>
                        <br/>
                    </td>
                </xsl:if>
                <xsl:if test="$varEczaciKar='1'">
                    <td class="lineTableTd" align="right">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine/cbc:Note">
                            <xsl:if test="contains(.,'ECK:') or contains(.,'EKO:')">
                                <xsl:text>%</xsl:text>
                                <xsl:value-of select="substring-after(substring(.,4),':')" />
                                <xsl:text> </xsl:text>
                            </xsl:if>
                        </xsl:for-each>
                    </td>
                </xsl:if>
                <xsl:if test="$varKurumIskonto='1'">
                    <td class="lineTableTd" align="right">
                        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine/cbc:Note">
                            <xsl:if test="contains(.,'KRI:')">
                                <xsl:value-of select="substring-after(substring(.,4),':')" />
                                <xsl:text> </xsl:text>
                                <xsl:if test="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRL&quot; or //n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID = &quot;TRY&quot;">
                                    <xsl:text>TL</xsl:text>
                                </xsl:if>
                                <xsl:if test="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRL&quot; and //n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID != &quot;TRY&quot;">
                                    <xsl:value-of select="//n1:Invoice/cac:InvoiceLine/cac:Price/cbc:PriceAmount/@currencyID"/>
                                </xsl:if>
                            </xsl:if>
                        </xsl:for-each>
                    </td>
                </xsl:if>
                <xsl:if test="$varVade='1'">
                    <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                        <xsl:for-each select="//n1:Invoice/cac:InvoiceLine/cbc:Note">
                            <xsl:if test="contains(.,'VAD:')">
                                <xsl:value-of select="substring-after(substring(.,4),':')" />
                                <xsl:text> </xsl:text>
                            </xsl:if>
                        </xsl:for-each>
                        <br/>
                    </td>
                </xsl:if>
                <xsl:if test="$varAllowanceRate &gt; 0">
                    <td class="lineTableTd" align="right">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:for-each select="./cac:AllowanceCharge/cbc:MultiplierFactorNumeric">
                            <xsl:choose>
                                <xsl:when test="../cbc:ChargeIndicator='true'">
                                    <xsl:text>(+) %</xsl:text>
                                </xsl:when>
                                <xsl:otherwise>
                                    <xsl:text>(-) %</xsl:text>
                                </xsl:otherwise>
                            </xsl:choose>
                            <xsl:value-of select="format-number(. * 100, '###.##0,00', 'european')"/>
                            <br/>
                        </xsl:for-each>
                    </td>
                </xsl:if>
                <xsl:if test="$varAllowanceAmount &gt; 0">
                    <td class="lineTableTd" align="right">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:for-each select="cac:AllowanceCharge/cbc:Amount">
                            <xsl:call-template name="Curr_Type"/>
                            <br/>
                        </xsl:for-each>
                    </td>
                </xsl:if>
                <xsl:if test="$varAllowanceReason &gt; 0">
                    <td class="lineTableTd" align="right">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:for-each select="cac:AllowanceCharge/cbc:AllowanceChargeReason">

                            <xsl:choose>
                                <xsl:when test="../cbc:ChargeIndicator='true'">
                                    <xsl:text>Arttırım - </xsl:text>
                                </xsl:when>
                                <xsl:otherwise>
                                    <xsl:text>İskonto - </xsl:text>
                                </xsl:otherwise>
                            </xsl:choose>
                            <xsl:apply-templates/>
                            <br/>
                        </xsl:for-each>
                    </td>
                </xsl:if>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each
                            select="./cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
                        <xsl:if test="cbc:TaxTypeCode='0015' ">
                            <xsl:text> </xsl:text>
                            <xsl:if test="../../cbc:Percent">
                                <xsl:text> %</xsl:text>
                                <xsl:value-of select="format-number(../../cbc:Percent, '###.##0,00', 'european')"
                                />
                            </xsl:if>
                        </xsl:if>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each
                            select="./cac:TaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
                        <xsl:if test="cbc:TaxTypeCode='0015' ">
                            <xsl:text> </xsl:text>
                            <xsl:for-each select="../../cbc:TaxAmount">
                                <xsl:call-template name="Curr_Type"/>
                            </xsl:for-each>
                        </xsl:if>
                    </xsl:for-each>
             

                 
                  
                    <xsl:for-each select="./cac:WithholdingTaxTotal/cac:TaxSubtotal/cac:TaxCategory/cac:TaxScheme">
                        <xsl:text>KDV TEVKİFAT </xsl:text>
                        <xsl:if test="../../cbc:Percent">
                            <xsl:text> (%</xsl:text>
                            <xsl:value-of
                                    select="format-number(../../cbc:Percent, '###.##0,00', 'european')"/>
                            <xsl:text>)=</xsl:text>
                        </xsl:if>
                        <xsl:for-each select="../../cbc:TaxAmount">
                            <xsl:call-template name="Curr_Type"/>
                            <xsl:text>&#10;</xsl:text>
                        </xsl:for-each>
                    </xsl:for-each>
                    <xsl:for-each select="./cbc:Note">
                        <xsl:if test="contains(.,'AVANS MAHSUBU')">
                            <br/>
                            <xsl:value-of select="." />
                        </xsl:if>
                        <xsl:if test="contains(.,'NAKİT TEMİNAT KESİNTİSİ')">
                            <br/>
                            <xsl:value-of select="." />
                        </xsl:if>
                    </xsl:for-each>
                </td>
        

                 
                   
              
            </xsl:if>
            <td class="lineTableTd" align="right">
                <xsl:text>&#160;</xsl:text>
                <xsl:for-each select="cbc:LineExtensionAmount">
                    <xsl:call-template name="Curr_Type"/>
                </xsl:for-each>
            </td>
            <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='TEVKIFATIADE'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="./cac:TaxTotal/cac:TaxSubtotal/cbc:TaxableAmount">
                        <xsl:call-template name="Curr_Type"/>
                    </xsl:for-each>
                </td>
            </xsl:if>
            <xsl:if test="//n1:Invoice/cbc:ProfileID='EARSIVFATURA' and //n1:Invoice/cbc:InvoiceTypeCode='ISTISNA'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:value-of select="cac:Delivery/cac:Shipment/cac:GoodsItem/cbc:RequiredCustomsID"/>
                </td>
            </xsl:if>
            <xsl:if test="//n1:Invoice/cbc:ProfileID='HKS' or /n1:Invoice/cbc:InvoiceTypeCode='HKSSATIS' or /n1:Invoice/cbc:InvoiceTypeCode='HKSKOMISYONCU'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each
                            select="cac:Item/cac:AdditionalItemIdentification/cbc:ID[@schemeID='KUNYENO']">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
            </xsl:if>
            <xsl:if test="//n1:Invoice/cbc:ProfileID='HKS' and /n1:Invoice/cbc:InvoiceTypeCode='SATIS'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each
                            select="cac:Item/cac:AdditionalItemIdentification/cbc:ID[@schemeID='MALSAHIBIVKNTCKN']">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each
                            select="cac:Item/cac:AdditionalItemIdentification/cbc:ID[@schemeID='MALSAHIBIADSOYADUNVAN']">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
            </xsl:if>
            <xsl:if test="//n1:Invoice/cbc:InvoiceTypeCode='HKSSATIS'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each
                            select="cac:Item/cac:AdditionalItemIdentification/cbc:ID[@schemeID='MALSAHIBIVKNTCKN']">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each
                            select="cac:Item/cac:AdditionalItemIdentification/cbc:ID[@schemeID='MALSAHIBIADSOYADUNVAN']">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
            </xsl:if>
            <xsl:if test="//n1:Invoice/cbc:ProfileID='IHRACAT' or //n1:Invoice/cbc:ProfileID='OZELFATURA'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="cac:Delivery/cac:DeliveryTerms/cbc:ID[@schemeID='INCOTERMS']">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="cac:Delivery/cac:Shipment/cac:TransportHandlingUnit/cac:ActualPackage/cbc:PackagingTypeCode">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:call-template name="Packaging">
                            <xsl:with-param name="PackagingType">
                                <xsl:value-of select="."/>
                            </xsl:with-param>
                        </xsl:call-template>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="cac:Delivery/cac:Shipment/cac:TransportHandlingUnit/cac:ActualPackage/cbc:ID">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:value-of
                            select="format-number(cac:Delivery/cac:Shipment/cac:TransportHandlingUnit/cac:ActualPackage/cbc:Quantity, '###.##0,00', 'european')"/>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="cac:Delivery/cac:DeliveryAddress">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="cac:Delivery/cac:Shipment/cac:ShipmentStage/cbc:TransportModeCode">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:call-template name="TransportMode">
                            <xsl:with-param name="TransportModeType">
                                <xsl:value-of select="."/>
                            </xsl:with-param>
                        </xsl:call-template>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="cac:Delivery/cac:Shipment/cac:GoodsItem/cbc:RequiredCustomsID">
                        <xsl:text>&#160;</xsl:text>
                        <xsl:apply-templates/>
                    </xsl:for-each>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                    <xsl:for-each select="cac:Delivery/cac:Shipment/cbc:DeclaredCustomsValueAmount">
                        <xsl:call-template name="Curr_Type"/>
                    </xsl:for-each>
                </td>
            </xsl:if>
        </tr>
    </xsl:template>
    <xsl:template match="//cbc:IssueDate">
        <xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
    </xsl:template>
    <xsl:template match="//cbc:StartDate">
        <xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
    </xsl:template>
    <xsl:template match="//cbc:EndDate">
        <xsl:value-of select="substring(.,9,2)"/>-<xsl:value-of select="substring(.,6,2)"/>-<xsl:value-of select="substring(.,1,4)"/>
    </xsl:template>
    <xsl:template match="//n1:Invoice">
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
            <td class="lineTableTd" align="right">
                <xsl:text>&#160;</xsl:text>
            </td>
            <xsl:if test="//n1:Invoice/cbc:ProfileID='HKS'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                </td>
            </xsl:if>
            <xsl:if test="//n1:Invoice/cbc:ProfileID='HKS' and /n1:Invoice/cbc:InvoiceTypeCode='SATIS'">
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                </td>
                <td class="lineTableTd" align="right">
                    <xsl:text>&#160;</xsl:text>
                </td>
            </xsl:if>
            <xsl:if test="//n1:Invoice/cbc:ProfileID='IHRACAT' or //n1:Invoice/cbc:ProfileID='OZELFATURA'">
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
            </xsl:if>
        </tr>
    </xsl:template>
    <xsl:template name="Party_Title" >
        <xsl:param name="PartyType" />
        <td style="width:469px; " align="left">
            <xsl:if test="cac:PartyName">
                <xsl:value-of select="cac:PartyName/cbc:Name"/>
                <br/>
            </xsl:if>
            <xsl:if test="cac:PartyLegalEntity">
                <xsl:text>Vergi No:</xsl:text>
                <xsl:value-of select="cac:PartyLegalEntity/cbc:CompanyID"/>
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
                    <xsl:for-each select="cbc:NationalityID">
                        <xsl:call-template name="Country">
                            <xsl:with-param name="CountryType">
                                <xsl:value-of select="."/>
                            </xsl:with-param>
                        </xsl:call-template>
                    </xsl:for-each>
                </xsl:if>
            </xsl:for-each>
        </td>
    </xsl:template>
    <xsl:template name="Party_Adress" >
        <xsl:param name="PartyType" />
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
                <xsl:for-each select="cac:Country/cbc:Name">
                    <br/>
                    <xsl:apply-templates/>
                    <br/>
                </xsl:for-each>
            </xsl:for-each>
        </td>
    </xsl:template>
    <xsl:template name="TransportMode">
        <xsl:param name="TransportModeType" />
        <xsl:choose>
            <xsl:when test="$TransportModeType=1">Denizyolu</xsl:when>
            <xsl:when test="$TransportModeType=2">Demiryolu</xsl:when>
            <xsl:when test="$TransportModeType=3">Karayolu</xsl:when>
            <xsl:when test="$TransportModeType=4">Havayolu</xsl:when>
            <xsl:when test="$TransportModeType=5">Posta</xsl:when>
            <xsl:when test="$TransportModeType=6">Çok araçlı</xsl:when>
            <xsl:when test="$TransportModeType=7">Sabit taşıma tesisleri</xsl:when>
            <xsl:when test="$TransportModeType=8">İç su taşımacılığı</xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$TransportModeType"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    <xsl:template name="PaymentMeansCode">
        <xsl:param name="PaymentMeansCodeType" />
        <xsl:choose>
            <xsl:when test="$PaymentMeansCodeType='1'">ODEME ARACISI</xsl:when>
            <xsl:when test="$PaymentMeansCodeType='10'">KAPIDA ODEME</xsl:when>
            <xsl:when test="$PaymentMeansCodeType='30'">EFT/HAVALE</xsl:when>
            <xsl:when test="$PaymentMeansCodeType='48'">KREDIKARTI/BANKAKARTI</xsl:when>
            <xsl:when test="$PaymentMeansCodeType='ZZZ'">Özel Tanımlı</xsl:when>
            <xsl:when test="$PaymentMeansCodeType='97'">DIGER</xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$PaymentMeansCodeType"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    <xsl:template name="Packaging">
        <xsl:param name="PackagingType" />
        <xsl:choose>
            <xsl:when test="$PackagingType='1A'">Çelik bidon</xsl:when>
            <xsl:when test="$PackagingType='1B'">Alüminyum bidon</xsl:when>
            <xsl:when test="$PackagingType='1D'">Kontraplak bidon</xsl:when>
            <xsl:when test="$PackagingType='1F'">Esnek ambalaj kutu</xsl:when>
            <xsl:when test="$PackagingType='1G'">Elyaflı silindir</xsl:when>
            <xsl:when test="$PackagingType='1W'">Ahşap silindir</xsl:when>
            <xsl:when test="$PackagingType='2C'">Ahşap varil</xsl:when>
            <xsl:when test="$PackagingType='3A'">Beş galonluk çelik bidon</xsl:when>
            <xsl:when test="$PackagingType='3H'">Beş galonluk plastik bidon</xsl:when>
            <xsl:when test="$PackagingType='43'">Torba, süper boy</xsl:when>
            <xsl:when test="$PackagingType='44'">Çoklu torba</xsl:when>
            <xsl:when test="$PackagingType='4A'">Çelik kutu</xsl:when>
            <xsl:when test="$PackagingType='4B'">Alüminyum kutu</xsl:when>
            <xsl:when test="$PackagingType='4C'">Doğal ahşap kutu</xsl:when>
            <xsl:when test="$PackagingType='4D'">Kontraplak kutu</xsl:when>
            <xsl:when test="$PackagingType='4F'">Yeniden üretilmiş ahşap kutu</xsl:when>
            <xsl:when test="$PackagingType='4G'">Elyaf tahta kutu</xsl:when>
            <xsl:when test="$PackagingType='4H'">Plastik kutu</xsl:when>
            <xsl:when test="$PackagingType='5H'">Plastik dokuma torba</xsl:when>
            <xsl:when test="$PackagingType='5L'">Kumaş torba</xsl:when>
            <xsl:when test="$PackagingType='5M'">Kağıt torba</xsl:when>
            <xsl:when test="$PackagingType='6H'">Kompozit ambalaj, plastik kap</xsl:when>
            <xsl:when test="$PackagingType='6P'">Kompozit ambalaj, cam kutu</xsl:when>
            <xsl:when test="$PackagingType='7A'">Araba kabı</xsl:when>
            <xsl:when test="$PackagingType='7B'">Ahşap kasa</xsl:when>
            <xsl:when test="$PackagingType='8A'">Ahşap palet</xsl:when>
            <xsl:when test="$PackagingType='8B'">Ahşap kasa</xsl:when>
            <xsl:when test="$PackagingType='8C'">Ahşap paketi</xsl:when>
            <xsl:when test="$PackagingType='AA'">Ortaboy sert plastik dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='AB'">Elyaf kap</xsl:when>
            <xsl:when test="$PackagingType='AC'">Kağıt kap</xsl:when>
            <xsl:when test="$PackagingType='AD'">Ahşap kap</xsl:when>
            <xsl:when test="$PackagingType='AE'">Aerosol</xsl:when>
            <xsl:when test="$PackagingType='AF'">Palet, modüler, yaka 80cms * 60cms</xsl:when>
            <xsl:when test="$PackagingType='AG'">Sarılmış palet</xsl:when>
            <xsl:when test="$PackagingType='AH'">Palet, 100 cms * 110 cms</xsl:when>
            <xsl:when test="$PackagingType='AI'">Çift çeneli kepçe</xsl:when>
            <xsl:when test="$PackagingType='AJ'">Koni</xsl:when>
            <xsl:when test="$PackagingType='AL'">Top</xsl:when>
            <xsl:when test="$PackagingType='AM'">Korumasız ampul</xsl:when>
            <xsl:when test="$PackagingType='AP'">Korumalı ampül</xsl:when>
            <xsl:when test="$PackagingType='AT'">Püskürteç</xsl:when>
            <xsl:when test="$PackagingType='AV'">Kapsül</xsl:when>
            <xsl:when test="$PackagingType='B4'">Kemer</xsl:when>
            <xsl:when test="$PackagingType='BA'">Varil</xsl:when>
            <xsl:when test="$PackagingType='BB'">Bobin</xsl:when>
            <xsl:when test="$PackagingType='BC'">Şişe kasası/rafı</xsl:when>
            <xsl:when test="$PackagingType='BD'">Tahta</xsl:when>
            <xsl:when test="$PackagingType='BE'">Bohça</xsl:when>
            <xsl:when test="$PackagingType='BF'">Balon, korunmasız</xsl:when>
            <xsl:when test="$PackagingType='BG'">Torba</xsl:when>
            <xsl:when test="$PackagingType='BH'">Demet</xsl:when>
            <xsl:when test="$PackagingType='BI'">Çöp kutusu</xsl:when>
            <xsl:when test="$PackagingType='BJ'">Kova</xsl:when>
            <xsl:when test="$PackagingType='BK'">Sepet</xsl:when>
            <xsl:when test="$PackagingType='BL'">Sıkıştırılmış balya</xsl:when>
            <xsl:when test="$PackagingType='BM'">Kase</xsl:when>
            <xsl:when test="$PackagingType='BN'">Sıkıştırılmamış balya</xsl:when>
            <xsl:when test="$PackagingType='BO'">Şişe, korunmasız, silindirik</xsl:when>
            <xsl:when test="$PackagingType='BP'">Balon, korunmasız</xsl:when>
            <xsl:when test="$PackagingType='BQ'">Şişe, korunmuş, silindirik</xsl:when>
            <xsl:when test="$PackagingType='BR'">Çubuk</xsl:when>
            <xsl:when test="$PackagingType='BS'">Şişe, korunmasız, soğanbiçim</xsl:when>
            <xsl:when test="$PackagingType='BT'">Sürgü</xsl:when>
            <xsl:when test="$PackagingType='BU'">İzmarit</xsl:when>
            <xsl:when test="$PackagingType='BV'">Şişe, korunmuş, soğanbiçim</xsl:when>
            <xsl:when test="$PackagingType='BW'">Sıvılar için kutu</xsl:when>
            <xsl:when test="$PackagingType='BX'">Kutu</xsl:when>
            <xsl:when test="$PackagingType='BY'">Tahta, paket halinde/demet</xsl:when>
            <xsl:when test="$PackagingType='BZ'">Çıbuklar, paket halinde/demet</xsl:when>
            <xsl:when test="$PackagingType='CA'">Dikdörtgen teneke</xsl:when>
            <xsl:when test="$PackagingType='CB'">Bira kasası</xsl:when>
            <xsl:when test="$PackagingType='CC'">Yayık</xsl:when>
            <xsl:when test="$PackagingType='CD'">Teneke ibrik</xsl:when>
            <xsl:when test="$PackagingType='CE'">Balık sepeti</xsl:when>
            <xsl:when test="$PackagingType='CF'">Sandık</xsl:when>
            <xsl:when test="$PackagingType='CG'">Kafes</xsl:when>
            <xsl:when test="$PackagingType='CH'">Sandık</xsl:when>
            <xsl:when test="$PackagingType='CI'">Teneke kutu</xsl:when>
            <xsl:when test="$PackagingType='CJ'">Tabut</xsl:when>
            <xsl:when test="$PackagingType='CK'">Fıçı</xsl:when>
            <xsl:when test="$PackagingType='CL'">Bobin</xsl:when>
            <xsl:when test="$PackagingType='CM'">Kart</xsl:when>
            <xsl:when test="$PackagingType='CN'">Konteyner</xsl:when>
            <xsl:when test="$PackagingType='CO'">Damacana, korumasız</xsl:when>
            <xsl:when test="$PackagingType='CP'">Damacana, korumalı</xsl:when>
            <xsl:when test="$PackagingType='CQ'">Kartuş</xsl:when>
            <xsl:when test="$PackagingType='CR'">Kasa</xsl:when>
            <xsl:when test="$PackagingType='CS'">Kutu</xsl:when>
            <xsl:when test="$PackagingType='CT'">Karton kutu</xsl:when>
            <xsl:when test="$PackagingType='CU'">Fincan</xsl:when>
            <xsl:when test="$PackagingType='CV'">Kapak</xsl:when>
            <xsl:when test="$PackagingType='CW'">Rulo kafes</xsl:when>
            <xsl:when test="$PackagingType='CX'">Silindirik teneke</xsl:when>
            <xsl:when test="$PackagingType='CY'">Silindir</xsl:when>
            <xsl:when test="$PackagingType='CZ'">Tuval</xsl:when>
            <xsl:when test="$PackagingType='DA'">Kasa, çok tabakalı, plastik</xsl:when>
            <xsl:when test="$PackagingType='DB'">Kasa, çok tabakalı, ahşap</xsl:when>
            <xsl:when test="$PackagingType='DC'">Kasa, çok tabakalı, karton</xsl:when>
            <xsl:when test="$PackagingType='DI'">Demir varil</xsl:when>
            <xsl:when test="$PackagingType='DJ'">Damacana</xsl:when>
            <xsl:when test="$PackagingType='DK'">Karton kasa</xsl:when>
            <xsl:when test="$PackagingType='DL'">Plastik dökme kasa</xsl:when>
            <xsl:when test="$PackagingType='DM'">Ahşap dökme kasa</xsl:when>
            <xsl:when test="$PackagingType='DN'">Sebil/dağıtıcı</xsl:when>
            <xsl:when test="$PackagingType='DP'">Damacana, korumalı</xsl:when>
            <xsl:when test="$PackagingType='DR'">Bidon</xsl:when>
            <xsl:when test="$PackagingType='DS'">Üst kapaksız plastik tepsi, tek tabaka</xsl:when>
            <xsl:when test="$PackagingType='DT'">Üst kapaksız ahşap tepsi, tek tabaka</xsl:when>
            <xsl:when test="$PackagingType='DU'">Üst kapaksız polistiren tepsi, tek
                tabaka</xsl:when>
            <xsl:when test="$PackagingType='DV'">Üst kapaksız karton tepsi, tek tabaka</xsl:when>
            <xsl:when test="$PackagingType='DW'">Üst kapaksız plastik tepsi, çift tabaka</xsl:when>
            <xsl:when test="$PackagingType='DX'"/>
            <xsl:when test="$PackagingType='DY'">Üst kapaksız karton tepsi, çift tabaka</xsl:when>
            <xsl:when test="$PackagingType='EC'">Plastik torba</xsl:when>
            <xsl:when test="$PackagingType='ED'">Kasa, palet tabanı ile</xsl:when>
            <xsl:when test="$PackagingType='EE'">Ahşap kasa, palet tabanı ile</xsl:when>
            <xsl:when test="$PackagingType='EF'">Karton kasa, palet tabanı ile</xsl:when>
            <xsl:when test="$PackagingType='EG'">Plastik kasa, palet tabanı ile</xsl:when>
            <xsl:when test="$PackagingType='EH'">Metal kasa, palet tabanı ile</xsl:when>
            <xsl:when test="$PackagingType='EI'">İzotermik kasa</xsl:when>
            <xsl:when test="$PackagingType='EN'">Zarf</xsl:when>
            <xsl:when test="$PackagingType='FB'">Plastik esnek torba</xsl:when>
            <xsl:when test="$PackagingType='FC'">Meyve kasası</xsl:when>
            <xsl:when test="$PackagingType='FD'">Çerçeveli kasa</xsl:when>
            <xsl:when test="$PackagingType='FE'">Plastik esnek depo</xsl:when>
            <xsl:when test="$PackagingType='FI'">Küçük fıçı</xsl:when>
            <xsl:when test="$PackagingType='FL'">Matara</xsl:when>
            <xsl:when test="$PackagingType='FO'">Küçük sandık</xsl:when>
            <xsl:when test="$PackagingType='FR'">Çerçeve</xsl:when>
            <xsl:when test="$PackagingType='FT'">Streçlenmiş yemek kabı</xsl:when>
            <xsl:when test="$PackagingType='FW'">Yanları üstü açık yük arabası</xsl:when>
            <xsl:when test="$PackagingType='FX'">Esnek torba</xsl:when>
            <xsl:when test="$PackagingType='GB'">Gaz şişesi</xsl:when>
            <xsl:when test="$PackagingType='GI'">Kiriş</xsl:when>
            <xsl:when test="$PackagingType='GL'">Konteyner, galon</xsl:when>
            <xsl:when test="$PackagingType='GR'">Cam kap</xsl:when>
            <xsl:when test="$PackagingType='GY'">Çul</xsl:when>
            <xsl:when test="$PackagingType='GZ'">Kiriş, demet/grup</xsl:when>
            <xsl:when test="$PackagingType='HA'">Saplı plastik sepet</xsl:when>
            <xsl:when test="$PackagingType='HB'">Saplı ahşap sepet</xsl:when>
            <xsl:when test="$PackagingType='HC'">Saplı karton sepet</xsl:when>
            <xsl:when test="$PackagingType='HG'">Büyük fıçı</xsl:when>
            <xsl:when test="$PackagingType='HN'">Askı</xsl:when>
            <xsl:when test="$PackagingType='HR'">Kapaklı sepet</xsl:when>
            <xsl:when test="$PackagingType='IA'">Ahşap sergi paketi</xsl:when>
            <xsl:when test="$PackagingType='IB'">Karton sergi paketi</xsl:when>
            <xsl:when test="$PackagingType='IC'">Plastik sergi paketi</xsl:when>
            <xsl:when test="$PackagingType='ID'">Metal sergi paketi</xsl:when>
            <xsl:when test="$PackagingType='IE'">Gösteri paketi</xsl:when>
            <xsl:when test="$PackagingType='IF'">Şeffaf oluklu paket</xsl:when>
            <xsl:when test="$PackagingType='IG'">Kağıt sarılı ambalaj</xsl:when>
            <xsl:when test="$PackagingType='IH'">Plastik bidon</xsl:when>
            <xsl:when test="$PackagingType='IK'">Şişe delikli karton paket</xsl:when>
            <xsl:when test="$PackagingType='IL'">Tepsi, katı, kapaklı istiflenebilir</xsl:when>
            <xsl:when test="$PackagingType='IN'">Külçe</xsl:when>
            <xsl:when test="$PackagingType='IZ'">Paket/grop halde külçe</xsl:when>
            <xsl:when test="$PackagingType='JB'">Jumbo boy torba</xsl:when>
            <xsl:when test="$PackagingType='JC'">Beş galonluk dikdörtgen bidon</xsl:when>
            <xsl:when test="$PackagingType='JG'">Sürahi</xsl:when>
            <xsl:when test="$PackagingType='JR'">Kavanoz</xsl:when>
            <xsl:when test="$PackagingType='JY'">Beş galonluk silindir bidon</xsl:when>
            <xsl:when test="$PackagingType='KI'">Takım</xsl:when>
            <xsl:when test="$PackagingType='LE'">Bagaj</xsl:when>
            <xsl:when test="$PackagingType='LG'">Kütük</xsl:when>
            <xsl:when test="$PackagingType='LT'">Pay</xsl:when>
            <xsl:when test="$PackagingType='LU'">Kulp</xsl:when>
            <xsl:when test="$PackagingType='LV'">Liftvan</xsl:when>
            <xsl:when test="$PackagingType='LZ'">Paket/grup kütükler</xsl:when>
            <xsl:when test="$PackagingType='MA'">Metal kasa</xsl:when>
            <xsl:when test="$PackagingType='MB'">Çoklu çanta</xsl:when>
            <xsl:when test="$PackagingType='MC'">Süt kasasu</xsl:when>
            <xsl:when test="$PackagingType='ME'">Metal konteyner</xsl:when>
            <xsl:when test="$PackagingType='MR'">Metal kap</xsl:when>
            <xsl:when test="$PackagingType='MS'">Çok duvarlı çuval</xsl:when>
            <xsl:when test="$PackagingType='MT'">Mat</xsl:when>
            <xsl:when test="$PackagingType='MW'">Plastik sarılmış kap</xsl:when>
            <xsl:when test="$PackagingType='MX'">Kibrit kutusu</xsl:when>
            <xsl:when test="$PackagingType='NE'">Ambalajsız</xsl:when>
            <xsl:when test="$PackagingType='NF'">Ambalajsız, tek ünite</xsl:when>
            <xsl:when test="$PackagingType='NG'">Ambalajsız, çok ünite</xsl:when>
            <xsl:when test="$PackagingType='NS'">Yuva</xsl:when>
            <xsl:when test="$PackagingType='NT'">Ağ</xsl:when>
            <xsl:when test="$PackagingType='NU'">Plastik ağ tüp</xsl:when>
            <xsl:when test="$PackagingType='NV'">Kumaş ağ tüp</xsl:when>
            <xsl:when test="$PackagingType='OA'">Palet, CHEP 40x60 cm</xsl:when>
            <xsl:when test="$PackagingType='OB'">Palet, CHEP 80x120 cm</xsl:when>
            <xsl:when test="$PackagingType='OC'">Palet, CHEP 100x120 cm</xsl:when>
            <xsl:when test="$PackagingType='OD'">Avustralya standart paleti</xsl:when>
            <xsl:when test="$PackagingType='OE'">Palet, 110x100 cm</xsl:when>
            <xsl:when test="$PackagingType='OF'">Nakliye platformu, belirtilmemiş ağırlık ve
                bıyut</xsl:when>
            <xsl:when test="$PackagingType='OK'">Blok</xsl:when>
            <xsl:when test="$PackagingType='OT'">Sekiz kenar kutu</xsl:when>
            <xsl:when test="$PackagingType='OU'">Dış konteyner</xsl:when>
            <xsl:when test="$PackagingType='P2'">Tava</xsl:when>
            <xsl:when test="$PackagingType='PA'">Küçük paket</xsl:when>
            <xsl:when test="$PackagingType='PB'">Kombine açık uçlu kutu ve palet</xsl:when>
            <xsl:when test="$PackagingType='PC'">Parsel</xsl:when>
            <xsl:when test="$PackagingType='PD'">Palet, modüler 80 x 100 cm</xsl:when>
            <xsl:when test="$PackagingType='PE'">Palet, modüler 80 x 120 cm</xsl:when>
            <xsl:when test="$PackagingType='PF'">Kalem</xsl:when>
            <xsl:when test="$PackagingType='PG'">Plaka</xsl:when>
            <xsl:when test="$PackagingType='PH'">Sürahi</xsl:when>
            <xsl:when test="$PackagingType='PI'">Boru</xsl:when>
            <xsl:when test="$PackagingType='PJ'">Meyve sepeti</xsl:when>
            <xsl:when test="$PackagingType='PK'">Paket</xsl:when>
            <xsl:when test="$PackagingType='PL'">Gerdel</xsl:when>
            <xsl:when test="$PackagingType='PN'">Kalas</xsl:when>
            <xsl:when test="$PackagingType='PO'">Destek</xsl:when>
            <xsl:when test="$PackagingType='PP'">Parça</xsl:when>
            <xsl:when test="$PackagingType='PR'">Plastik kap</xsl:when>
            <xsl:when test="$PackagingType='PT'">Demlik</xsl:when>
            <xsl:when test="$PackagingType='PU'">Tepsi</xsl:when>
            <xsl:when test="$PackagingType='PV'">Paket/grup boru</xsl:when>
            <xsl:when test="$PackagingType='PX'">Palet</xsl:when>
            <xsl:when test="$PackagingType='PY'">Paket/grup tabak</xsl:when>
            <xsl:when test="$PackagingType='PZ'">Paket/grup kalas</xsl:when>
            <xsl:when test="$PackagingType='QA'">Üstü açılmaz çelik bidon</xsl:when>
            <xsl:when test="$PackagingType='QB'">Üstü açılır çelik bidon</xsl:when>
            <xsl:when test="$PackagingType='QC'">Üstü açılmaz alüminyum bidon</xsl:when>
            <xsl:when test="$PackagingType='QD'">Üstü açılır alüminyum bidon</xsl:when>
            <xsl:when test="$PackagingType='QF'">Üstü açılmaz plastik bidon</xsl:when>
            <xsl:when test="$PackagingType='QG'">Üstü açılır plastik bidon</xsl:when>
            <xsl:when test="$PackagingType='QH'">Ahşap tıkaçlı varil</xsl:when>
            <xsl:when test="$PackagingType='QJ'">Üstü açılır ahşap varil</xsl:when>
            <xsl:when test="$PackagingType='QK'">Üstü açılmaz beş galonluk çelik bidon</xsl:when>
            <xsl:when test="$PackagingType='QL'">Üstü açılır beş galonluk çelik bidon</xsl:when>
            <xsl:when test="$PackagingType='QM'">Üstü açılmaz beş galonluk plastik bidon</xsl:when>
            <xsl:when test="$PackagingType='QN'">Üstü açılır beş galonluk plastik bidon</xsl:when>
            <xsl:when test="$PackagingType='QP'">Doğal ahşap kutu</xsl:when>
            <xsl:when test="$PackagingType='QQ'">Emniyet duvarlı doğal ahşap kutu</xsl:when>
            <xsl:when test="$PackagingType='QR'">Genişletilmiş plastik kutu</xsl:when>
            <xsl:when test="$PackagingType='QS'">Yekpare plastik kutu</xsl:when>
            <xsl:when test="$PackagingType='RD'">Çubuk</xsl:when>
            <xsl:when test="$PackagingType='RG'">Halka</xsl:when>
            <xsl:when test="$PackagingType='RJ'">Raf, elbise askısı</xsl:when>
            <xsl:when test="$PackagingType='RK'">Raf</xsl:when>
            <xsl:when test="$PackagingType='RL'">Makara</xsl:when>
            <xsl:when test="$PackagingType='RO'">Rulo</xsl:when>
            <xsl:when test="$PackagingType='RZ'">Paket/grup çubuk</xsl:when>
            <xsl:when test="$PackagingType='SA'">Çuval</xsl:when>
            <xsl:when test="$PackagingType='SB'">Levha</xsl:when>
            <xsl:when test="$PackagingType='SC'">Sığ kasa</xsl:when>
            <xsl:when test="$PackagingType='SD'">İğ</xsl:when>
            <xsl:when test="$PackagingType='SE'">Deniz sandığı</xsl:when>
            <xsl:when test="$PackagingType='SH'">Kesecik</xsl:when>
            <xsl:when test="$PackagingType='SI'">Kızak</xsl:when>
            <xsl:when test="$PackagingType='SK'">İskelet kasa</xsl:when>
            <xsl:when test="$PackagingType='SL'">Taşıma paleti</xsl:when>
            <xsl:when test="$PackagingType='SM'">Sac</xsl:when>
            <xsl:when test="$PackagingType='SO'">Tel/kablo/iplik makarası</xsl:when>
            <xsl:when test="$PackagingType='SP'">Plastik levha</xsl:when>
            <xsl:when test="$PackagingType='SS'">Çelik kasa</xsl:when>
            <xsl:when test="$PackagingType='ST'">Yaprak</xsl:when>
            <xsl:when test="$PackagingType='SU'">Bavul</xsl:when>
            <xsl:when test="$PackagingType='SV'">Çelik zarf</xsl:when>
            <xsl:when test="$PackagingType='SW'">Vakumlu ambalaj</xsl:when>
            <xsl:when test="$PackagingType='SX'">Set</xsl:when>
            <xsl:when test="$PackagingType='SY'">Kılıf</xsl:when>
            <xsl:when test="$PackagingType='SZ'">Paket/grup yaprak</xsl:when>
            <xsl:when test="$PackagingType='T1'">Tablet</xsl:when>
            <xsl:when test="$PackagingType='TB'">Küvet</xsl:when>
            <xsl:when test="$PackagingType='TC'">Çay sandığı</xsl:when>
            <xsl:when test="$PackagingType='TD'">Sıkılabilir tüp</xsl:when>
            <xsl:when test="$PackagingType='TE'">Lastik</xsl:when>
            <xsl:when test="$PackagingType='TG'">Genel tank konteynerı</xsl:when>
            <xsl:when test="$PackagingType='TI'"/>
            <xsl:when test="$PackagingType='TK'">Dikdörtgen tank</xsl:when>
            <xsl:when test="$PackagingType='TN'">Teneke</xsl:when>
            <xsl:when test="$PackagingType='TO'">Şarap fıçısı</xsl:when>
            <xsl:when test="$PackagingType='TR'">Gövde</xsl:when>
            <xsl:when test="$PackagingType='TS'">Bağ</xsl:when>
            <xsl:when test="$PackagingType='TU'">Tüp</xsl:when>
            <xsl:when test="$PackagingType='TV'">Enjektörlü tüp</xsl:when>
            <xsl:when test="$PackagingType='TY'">Silindirik tank</xsl:when>
            <xsl:when test="$PackagingType='TZ'">Paket/grup tüpler</xsl:when>
            <xsl:when test="$PackagingType='UN'">Birim</xsl:when>
            <xsl:when test="$PackagingType='VG'">Dökme gaz</xsl:when>
            <xsl:when test="$PackagingType='VI'">Küçük şişe</xsl:when>
            <xsl:when test="$PackagingType='VL'">Dökme sıvı</xsl:when>
            <xsl:when test="$PackagingType='VO'">Dökme katı</xsl:when>
            <xsl:when test="$PackagingType='VP'">Vakumlu</xsl:when>
            <xsl:when test="$PackagingType='VQ'">Dökme sıvılaştırılmış gaz</xsl:when>
            <xsl:when test="$PackagingType='VN'">Araç</xsl:when>
            <xsl:when test="$PackagingType='VR'">Dökme katı granül</xsl:when>
            <xsl:when test="$PackagingType='VS'">Dökme metal hurda</xsl:when>
            <xsl:when test="$PackagingType='VY'">Dökme ince parçacıklar</xsl:when>
            <xsl:when test="$PackagingType='WA'">Ortaboy dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WB'">Hasırlı şişe</xsl:when>
            <xsl:when test="$PackagingType='WC'">Ortaboy çelik dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WD'">Ortaboy alüminyum dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WF'">Ortaboy metal dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WK'">Sıvılar için ortaboy çelik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WL'">Sıvılar için ortaboy alümünyum dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WM'">Sıvılar için ortaboy metal dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WN'">Ortaboy iç astarsız örme plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WR'">Ortaboy iç astarlı örme plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WS'">Ortaboy plastik film dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WT'">Ortaboy iç astarsız kumaş plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WU'">Ortaboy iç astarlı doğal ahşap dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WX'">Ortaboy iç astarlı kumaş dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WY'">Ortaboy iç astarlı kontraplak dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='WZ'">Ortaboy iç astarlı sunta dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='XA'">İç astarsız örme plastik torba</xsl:when>
            <xsl:when test="$PackagingType='XB'">Sızdırmaz örme plastik torba</xsl:when>
            <xsl:when test="$PackagingType='XC'">Su geçirmez örme plastik torba</xsl:when>
            <xsl:when test="$PackagingType='XD'">Plastik film torba</xsl:when>
            <xsl:when test="$PackagingType='XF'">İç astarsız kumaş torba</xsl:when>
            <xsl:when test="$PackagingType='XG'">Sızdırmaz kumaş torba</xsl:when>
            <xsl:when test="$PackagingType='XH'">Su geçirmez kumaş torba</xsl:when>
            <xsl:when test="$PackagingType='XJ'">Çok duvarlı kağıt torba</xsl:when>
            <xsl:when test="$PackagingType='XK'">Su geçirmez çok duvarlı kağıt torba</xsl:when>
            <xsl:when test="$PackagingType='YA'">Kompozit ambalaj, çelik bidon içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YB'">Kompozit ambalaj, çelik kasa içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YC'">Kompozit ambalaj, alüminyum bidon içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YD'">Kompozit ambalaj, alüminyum kasa içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YF'">Kompozit ambalaj, ahşap kutu içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YG'">Kompozit ambalaj, kontraplak bidon içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YH'">Kompozit ambalaj, kontraplak kasa içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YJ'">Kompozit ambalaj, elyaf bidon içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YK'">Kompozit ambalaj, elyaf levha kasa içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YL'">Kompozit ambalaj, plastik bidon içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YM'">Kompozit ambalaj, yekpare plastik kasa içindeki
                plastik kap</xsl:when>
            <xsl:when test="$PackagingType='YN'">Kompozit ambalaj, çelik bidon içindeki cam
                kap</xsl:when>
            <xsl:when test="$PackagingType='YP'">Kompozit ambalaj, elyaf levha kasa içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YQ'">Kompozit ambalaj, alüminyum bidon içindeki cam
                kap</xsl:when>
            <xsl:when test="$PackagingType='YR'">Kompozit ambalaj, alüminyum kasa içindeki plastik
                kap</xsl:when>
            <xsl:when test="$PackagingType='YS'">Kompozit ambalaj, ahşap kasa içindeki cam
                kap</xsl:when>
            <xsl:when test="$PackagingType='YT'">Kompozit ambalaj, kontraplak bidon içindeki cam
                kap</xsl:when>
            <xsl:when test="$PackagingType='YV'">Kompozit ambalaj, hasır sepet içindeki cam
                kap</xsl:when>
            <xsl:when test="$PackagingType='YW'">Kompozit ambalaj, elyaf bidon içindeki cam
                kap</xsl:when>
            <xsl:when test="$PackagingType='YX'">Kompozit ambalaj, elyaf levha kasa içindeki cam
                kap</xsl:when>
            <xsl:when test="$PackagingType='YY'">Kompozit ambalaj, genişleyebilir plastik paket
                içindeki cam kap</xsl:when>
            <xsl:when test="$PackagingType='YZ'">Kompozit ambalaj, yekpare plastik paket içindeki
                cam kap</xsl:when>
            <xsl:when test="$PackagingType='ZA'">Ortaboy çok duvarlı kağıt dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZB'">Büyük boy torba</xsl:when>
            <xsl:when test="$PackagingType='ZC'">Ortaboy çok duvarlı su geçirmez kağıt dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZL'">Ortaboy kompozit yekpare sert plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZM'">Ortaboy kompozit yekpare esnek plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZN'">Ortaboy kompozit sıkıştırılmış sert plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZP'">Ortaboy kompozit sıkıştırılmış esnek plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZQ'">Sıvılar için ortaboy kompozit sert plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZR'">Sıvılar için ortaboy kompozit esnek plastik dolum
                konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZS'">Ortaboy kompozit dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZT'">Ortaboy elyaf levha dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZU'">Ortaboy esnek dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZW'">Ortaboy doğal ahşap dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZX'">Ortaboy kontraplak dolum konteynerı</xsl:when>
            <xsl:when test="$PackagingType='ZY'">Ortaboy sunta dolum konteynerı</xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$PackagingType"/>
            </xsl:otherwise>
        </xsl:choose>
    </xsl:template>
    <xsl:template name="Country">
        <xsl:param name="CountryType" />
        <xsl:choose>
            <xsl:when test="$CountryType='AF'">Afganistan</xsl:when>
            <xsl:when test="$CountryType='DE'">Almanya</xsl:when>
            <xsl:when test="$CountryType='AD'">Andorra</xsl:when>
            <xsl:when test="$CountryType='AO'">Angola</xsl:when>
            <xsl:when test="$CountryType='AG'">Antigua ve Barbuda</xsl:when>
            <xsl:when test="$CountryType='AR'">Arjantin</xsl:when>
            <xsl:when test="$CountryType='AL'">Arnavutluk</xsl:when>
            <xsl:when test="$CountryType='AW'">Aruba</xsl:when>
            <xsl:when test="$CountryType='AU'">Avustralya</xsl:when>
            <xsl:when test="$CountryType='AT'">Avusturya</xsl:when>
            <xsl:when test="$CountryType='AZ'">Azerbaycan</xsl:when>
            <xsl:when test="$CountryType='BS'">Bahamalar</xsl:when>
            <xsl:when test="$CountryType='BH'">Bahreyn</xsl:when>
            <xsl:when test="$CountryType='BD'">Bangladeş</xsl:when>
            <xsl:when test="$CountryType='BB'">Barbados</xsl:when>
            <xsl:when test="$CountryType='EH'">Batı Sahra (MA)</xsl:when>
            <xsl:when test="$CountryType='BE'">Belçika</xsl:when>
            <xsl:when test="$CountryType='BZ'">Belize</xsl:when>
            <xsl:when test="$CountryType='BJ'">Benin</xsl:when>
            <xsl:when test="$CountryType='BM'">Bermuda</xsl:when>
            <xsl:when test="$CountryType='BY'">Beyaz Rusya</xsl:when>
            <xsl:when test="$CountryType='BT'">Bhutan</xsl:when>
            <xsl:when test="$CountryType='AE'">Birleşik Arap Emirlikleri</xsl:when>
            <xsl:when test="$CountryType='US'">Birleşik Devletler</xsl:when>
            <xsl:when test="$CountryType='GB'">Birleşik Krallık</xsl:when>
            <xsl:when test="$CountryType='BO'">Bolivya</xsl:when>
            <xsl:when test="$CountryType='BA'">Bosna-Hersek</xsl:when>
            <xsl:when test="$CountryType='BW'">Botsvana</xsl:when>
            <xsl:when test="$CountryType='BR'">Brezilya</xsl:when>
            <xsl:when test="$CountryType='BN'">Bruney</xsl:when>
            <xsl:when test="$CountryType='BG'">Bulgaristan</xsl:when>
            <xsl:when test="$CountryType='BF'">Burkina Faso</xsl:when>
            <xsl:when test="$CountryType='BI'">Burundi</xsl:when>
            <xsl:when test="$CountryType='TD'">Çad</xsl:when>
            <xsl:when test="$CountryType='KY'">Cayman Adaları</xsl:when>
            <xsl:when test="$CountryType='GI'">Cebelitarık (GB)</xsl:when>
            <xsl:when test="$CountryType='CZ'">Çek Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='DZ'">Cezayir</xsl:when>
            <xsl:when test="$CountryType='DJ'">Cibuti</xsl:when>
            <xsl:when test="$CountryType='CN'">Çin</xsl:when>
            <xsl:when test="$CountryType='DK'">Danimarka</xsl:when>
            <xsl:when test="$CountryType='CD'">Demokratik Kongo Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='TL'">Doğu Timor</xsl:when>
            <xsl:when test="$CountryType='DO'">Dominik Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='DM'">Dominika</xsl:when>
            <xsl:when test="$CountryType='EC'">Ekvador</xsl:when>
            <xsl:when test="$CountryType='GQ'">Ekvator Ginesi</xsl:when>
            <xsl:when test="$CountryType='SV'">El Salvador</xsl:when>
            <xsl:when test="$CountryType='ID'">Endonezya</xsl:when>
            <xsl:when test="$CountryType='ER'">Eritre</xsl:when>
            <xsl:when test="$CountryType='AM'">Ermenistan</xsl:when>
            <xsl:when test="$CountryType='MF'">Ermiş Martin (FR)</xsl:when>
            <xsl:when test="$CountryType='EE'">Estonya</xsl:when>
            <xsl:when test="$CountryType='ET'">Etiyopya</xsl:when>
            <xsl:when test="$CountryType='FK'">Falkland Adaları</xsl:when>
            <xsl:when test="$CountryType='FO'">Faroe Adaları (DK)</xsl:when>
            <xsl:when test="$CountryType='MA'">Fas</xsl:when>
            <xsl:when test="$CountryType='FJ'">Fiji</xsl:when>
            <xsl:when test="$CountryType='CI'">Fildişi Sahili</xsl:when>
            <xsl:when test="$CountryType='PH'">Filipinler</xsl:when>
            <xsl:when test="$CountryType='FI'">Finlandiya</xsl:when>
            <xsl:when test="$CountryType='FR'">Fransa</xsl:when>
            <xsl:when test="$CountryType='GF'">Fransız Guyanası (FR)</xsl:when>
            <xsl:when test="$CountryType='PF'">Fransız Polinezyası (FR)</xsl:when>
            <xsl:when test="$CountryType='GA'">Gabon</xsl:when>
            <xsl:when test="$CountryType='GM'">Gambiya</xsl:when>
            <xsl:when test="$CountryType='GH'">Gana</xsl:when>
            <xsl:when test="$CountryType='GN'">Gine</xsl:when>
            <xsl:when test="$CountryType='GW'">Gine Bissau</xsl:when>
            <xsl:when test="$CountryType='GD'">Grenada</xsl:when>
            <xsl:when test="$CountryType='GL'">Grönland (DK)</xsl:when>
            <xsl:when test="$CountryType='GP'">Guadeloupe (FR)</xsl:when>
            <xsl:when test="$CountryType='GT'">Guatemala</xsl:when>
            <xsl:when test="$CountryType='GG'">Guernsey (GB)</xsl:when>
            <xsl:when test="$CountryType='ZA'">Güney Afrika</xsl:when>
            <xsl:when test="$CountryType='KR'">Güney Kore</xsl:when>
            <xsl:when test="$CountryType='GE'">Gürcistan</xsl:when>
            <xsl:when test="$CountryType='GY'">Guyana</xsl:when>
            <xsl:when test="$CountryType='HT'">Haiti</xsl:when>
            <xsl:when test="$CountryType='IN'">Hindistan</xsl:when>
            <xsl:when test="$CountryType='HR'">Hırvatistan</xsl:when>
            <xsl:when test="$CountryType='NL'">Hollanda</xsl:when>
            <xsl:when test="$CountryType='HN'">Honduras</xsl:when>
            <xsl:when test="$CountryType='HK'">Hong Kong (CN)</xsl:when>
            <xsl:when test="$CountryType='VG'">İngiliz Virjin Adaları</xsl:when>
            <xsl:when test="$CountryType='IQ'">Irak</xsl:when>
            <xsl:when test="$CountryType='IR'">İran</xsl:when>
            <xsl:when test="$CountryType='IE'">İrlanda</xsl:when>
            <xsl:when test="$CountryType='ES'">İspanya</xsl:when>
            <xsl:when test="$CountryType='IL'">İsrail</xsl:when>
            <xsl:when test="$CountryType='SE'">İsveç</xsl:when>
            <xsl:when test="$CountryType='CH'">İsviçre</xsl:when>
            <xsl:when test="$CountryType='IT'">İtalya</xsl:when>
            <xsl:when test="$CountryType='IS'">İzlanda</xsl:when>
            <xsl:when test="$CountryType='JM'">Jamaika</xsl:when>
            <xsl:when test="$CountryType='JP'">Japonya</xsl:when>
            <xsl:when test="$CountryType='JE'">Jersey (GB)</xsl:when>
            <xsl:when test="$CountryType='KH'">Kamboçya</xsl:when>
            <xsl:when test="$CountryType='CM'">Kamerun</xsl:when>
            <xsl:when test="$CountryType='CA'">Kanada</xsl:when>
            <xsl:when test="$CountryType='ME'">Karadağ</xsl:when>
            <xsl:when test="$CountryType='QA'">Katar</xsl:when>
            <xsl:when test="$CountryType='KZ'">Kazakistan</xsl:when>
            <xsl:when test="$CountryType='KE'">Kenya</xsl:when>
            <xsl:when test="$CountryType='CY'">Kıbrıs</xsl:when>
            <xsl:when test="$CountryType='KG'">Kırgızistan</xsl:when>
            <xsl:when test="$CountryType='KI'">Kiribati</xsl:when>
            <xsl:when test="$CountryType='CO'">Kolombiya</xsl:when>
            <xsl:when test="$CountryType='KM'">Komorlar</xsl:when>
            <xsl:when test="$CountryType='CG'">Kongo Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='KV'">Kosova (RS)</xsl:when>
            <xsl:when test="$CountryType='CR'">Kosta Rika</xsl:when>
            <xsl:when test="$CountryType='CU'">Küba</xsl:when>
            <xsl:when test="$CountryType='KW'">Kuveyt</xsl:when>
            <xsl:when test="$CountryType='KP'">Kuzey Kore</xsl:when>
            <xsl:when test="$CountryType='LA'">Laos</xsl:when>
            <xsl:when test="$CountryType='LS'">Lesoto</xsl:when>
            <xsl:when test="$CountryType='LV'">Letonya</xsl:when>
            <xsl:when test="$CountryType='LR'">Liberya</xsl:when>
            <xsl:when test="$CountryType='LY'">Libya</xsl:when>
            <xsl:when test="$CountryType='LI'">Lihtenştayn</xsl:when>
            <xsl:when test="$CountryType='LT'">Litvanya</xsl:when>
            <xsl:when test="$CountryType='LB'">Lübnan</xsl:when>
            <xsl:when test="$CountryType='LU'">Lüksemburg</xsl:when>
            <xsl:when test="$CountryType='HU'">Macaristan</xsl:when>
            <xsl:when test="$CountryType='MG'">Madagaskar</xsl:when>
            <xsl:when test="$CountryType='MO'">Makao (CN)</xsl:when>
            <xsl:when test="$CountryType='MK'">Makedonya</xsl:when>
            <xsl:when test="$CountryType='MW'">Malavi</xsl:when>
            <xsl:when test="$CountryType='MV'">Maldivler</xsl:when>
            <xsl:when test="$CountryType='MY'">Malezya</xsl:when>
            <xsl:when test="$CountryType='ML'">Mali</xsl:when>
            <xsl:when test="$CountryType='MT'">Malta</xsl:when>
            <xsl:when test="$CountryType='IM'">Man Adası (GB)</xsl:when>
            <xsl:when test="$CountryType='MH'">Marshall Adaları</xsl:when>
            <xsl:when test="$CountryType='MQ'">Martinique (FR)</xsl:when>
            <xsl:when test="$CountryType='MU'">Mauritius</xsl:when>
            <xsl:when test="$CountryType='YT'">Mayotte (FR)</xsl:when>
            <xsl:when test="$CountryType='MX'">Meksika</xsl:when>
            <xsl:when test="$CountryType='FM'">Mikronezya</xsl:when>
            <xsl:when test="$CountryType='EG'">Mısır</xsl:when>
            <xsl:when test="$CountryType='MN'">Moğolistan</xsl:when>
            <xsl:when test="$CountryType='MD'">Moldova</xsl:when>
            <xsl:when test="$CountryType='MC'">Monako</xsl:when>
            <xsl:when test="$CountryType='MR'">Moritanya</xsl:when>
            <xsl:when test="$CountryType='MZ'">Mozambik</xsl:when>
            <xsl:when test="$CountryType='MM'">Myanmar</xsl:when>
            <xsl:when test="$CountryType='NA'">Namibya</xsl:when>
            <xsl:when test="$CountryType='NR'">Nauru</xsl:when>
            <xsl:when test="$CountryType='NP'">Nepal</xsl:when>
            <xsl:when test="$CountryType='NE'">Nijer</xsl:when>
            <xsl:when test="$CountryType='NG'">Nijerya</xsl:when>
            <xsl:when test="$CountryType='NI'">Nikaragua</xsl:when>
            <xsl:when test="$CountryType='NO'">Norveç</xsl:when>
            <xsl:when test="$CountryType='CF'">Orta Afrika Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='UZ'">Özbekistan</xsl:when>
            <xsl:when test="$CountryType='PK'">Pakistan</xsl:when>
            <xsl:when test="$CountryType='PW'">Palau</xsl:when>
            <xsl:when test="$CountryType='PA'">Panama</xsl:when>
            <xsl:when test="$CountryType='PG'">Papua Yeni Gine</xsl:when>
            <xsl:when test="$CountryType='PY'">Paraguay</xsl:when>
            <xsl:when test="$CountryType='PE'">Peru</xsl:when>
            <xsl:when test="$CountryType='PL'">Polonya</xsl:when>
            <xsl:when test="$CountryType='PT'">Portekiz</xsl:when>
            <xsl:when test="$CountryType='PR'">Porto Riko (US)</xsl:when>
            <xsl:when test="$CountryType='RE'">Réunion (FR)</xsl:when>
            <xsl:when test="$CountryType='RO'">Romanya</xsl:when>
            <xsl:when test="$CountryType='RW'">Ruanda</xsl:when>
            <xsl:when test="$CountryType='RU'">Rusya</xsl:when>
            <xsl:when test="$CountryType='BL'">Saint Barthélemy (FR)</xsl:when>
            <xsl:when test="$CountryType='KN'">Saint Kitts ve Nevis</xsl:when>
            <xsl:when test="$CountryType='LC'">Saint Lucia</xsl:when>
            <xsl:when test="$CountryType='PM'">Saint Pierre ve Miquelon (FR)</xsl:when>
            <xsl:when test="$CountryType='VC'">Saint Vincent ve Grenadinler</xsl:when>
            <xsl:when test="$CountryType='WS'">Samoa</xsl:when>
            <xsl:when test="$CountryType='SM'">San Marino</xsl:when>
            <xsl:when test="$CountryType='ST'">São Tomé ve Príncipe</xsl:when>
            <xsl:when test="$CountryType='SN'">Senegal</xsl:when>
            <xsl:when test="$CountryType='SC'">Seyşeller</xsl:when>
            <xsl:when test="$CountryType='SL'">Sierra Leone</xsl:when>
            <xsl:when test="$CountryType='CL'">Şili</xsl:when>
            <xsl:when test="$CountryType='SG'">Singapur</xsl:when>
            <xsl:when test="$CountryType='RS'">Sırbistan</xsl:when>
            <xsl:when test="$CountryType='SK'">Slovakya Cumhuriyeti</xsl:when>
            <xsl:when test="$CountryType='SI'">Slovenya</xsl:when>
            <xsl:when test="$CountryType='SB'">Solomon Adaları</xsl:when>
            <xsl:when test="$CountryType='SO'">Somali</xsl:when>
            <xsl:when test="$CountryType='SS'">South Sudan</xsl:when>
            <xsl:when test="$CountryType='SJ'">Spitsbergen (NO)</xsl:when>
            <xsl:when test="$CountryType='LK'">Sri Lanka</xsl:when>
            <xsl:when test="$CountryType='SD'">Sudan</xsl:when>
            <xsl:when test="$CountryType='SR'">Surinam</xsl:when>
            <xsl:when test="$CountryType='SY'">Suriye</xsl:when>
            <xsl:when test="$CountryType='SA'">Suudi Arabistan</xsl:when>
            <xsl:when test="$CountryType='SZ'">Svaziland</xsl:when>
            <xsl:when test="$CountryType='TJ'">Tacikistan</xsl:when>
            <xsl:when test="$CountryType='TZ'">Tanzanya</xsl:when>
            <xsl:when test="$CountryType='TH'">Tayland</xsl:when>
            <xsl:when test="$CountryType='TW'">Tayvan</xsl:when>
            <xsl:when test="$CountryType='TG'">Togo</xsl:when>
            <xsl:when test="$CountryType='TO'">Tonga</xsl:when>
            <xsl:when test="$CountryType='TT'">Trinidad ve Tobago</xsl:when>
            <xsl:when test="$CountryType='TN'">Tunus</xsl:when>
            <xsl:when test="$CountryType='TR'">Türkiye</xsl:when>
            <xsl:when test="$CountryType='TM'">Türkmenistan</xsl:when>
            <xsl:when test="$CountryType='TC'">Turks ve Caicos</xsl:when>
            <xsl:when test="$CountryType='TV'">Tuvalu</xsl:when>
            <xsl:when test="$CountryType='UG'">Uganda</xsl:when>
            <xsl:when test="$CountryType='UA'">Ukrayna</xsl:when>
            <xsl:when test="$CountryType='OM'">Umman</xsl:when>
            <xsl:when test="$CountryType='JO'">Ürdün</xsl:when>
            <xsl:when test="$CountryType='UY'">Uruguay</xsl:when>
            <xsl:when test="$CountryType='VU'">Vanuatu</xsl:when>
            <xsl:when test="$CountryType='VA'">Vatikan</xsl:when>
            <xsl:when test="$CountryType='VE'">Venezuela</xsl:when>
            <xsl:when test="$CountryType='VN'">Vietnam</xsl:when>
            <xsl:when test="$CountryType='WF'">Wallis ve Futuna (FR)</xsl:when>
            <xsl:when test="$CountryType='YE'">Yemen</xsl:when>
            <xsl:when test="$CountryType='NC'">Yeni Kaledonya (FR)</xsl:when>
            <xsl:when test="$CountryType='NZ'">Yeni Zelanda</xsl:when>
            <xsl:when test="$CountryType='CV'">Yeşil Burun Adaları</xsl:when>
            <xsl:when test="$CountryType='GR'">Yunanistan</xsl:when>
            <xsl:when test="$CountryType='ZM'">Zambiya</xsl:when>
            <xsl:when test="$CountryType='ZW'">Zimbabve</xsl:when>
            <xsl:otherwise>
                <xsl:value-of select="$CountryType"/>
            </xsl:otherwise>
        </xsl:choose>

    </xsl:template>
    <xsl:template name='Party_Other'>
        <xsl:param name="PartyType" />
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
        <xsl:if test="$PartyType!='TAXFREE' and not(starts-with($PartyType, 'EXPORT'))">
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
            <xsl:for-each select="cac:AgentParty/cac:PartyIdentification">
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