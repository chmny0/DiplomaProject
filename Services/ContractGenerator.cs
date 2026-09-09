using System;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace WpfPlannerApp.Services
{
    public static class ContractGenerator
    {
        public static void GenerateContract(string filePath, string contractNumber, string companyName, string director, string workTypeName,
            string address, string appointmentDate, string appointmentTime, string inn, string kpp, string legalAddress)
        {
            using var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.Append(body);

            body.Append(CreateParagraph("ДОГОВОР № " + contractNumber, true, 28, true));
            body.Append(CreateParagraph("на оказание услуг связи", false, 24, true));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph($"                                               {DateTime.Now:dd MMMM yyyy} г.", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("ООО «Провайдер-Сервис», именуемое в дальнейшем «Исполнитель», в лице директора Петрова П.П., действующего на основании Устава, с одной стороны, и", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph($"{companyName}, именуемое в дальнейшем «Заказчик», в лице {director}, с другой стороны, заключили настоящий договор о нижеследующем:", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("1. ПРЕДМЕТ ДОГОВОРА", true, 24));
            body.Append(CreateParagraph($"1.1. Исполнитель обязуется оказать услуги по подключению: {workTypeName}, а Заказчик обязуется принять и оплатить оказанные услуги.", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("2. АДРЕС И СРОКИ ОКАЗАНИЯ УСЛУГ", true, 24));
            body.Append(CreateParagraph($"2.1. Адрес оказания услуг: {address}", false, 22));
            body.Append(CreateParagraph($"2.2. Дата и время выполнения работ: {appointmentDate} в {appointmentTime}", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("3. СТОИМОСТЬ УСЛУГ И ПОРЯДОК РАСЧЁТОВ", true, 24));
            body.Append(CreateParagraph("3.1. Стоимость услуг определяется согласно действующим тарифам Исполнителя.", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("4. РЕКВИЗИТЫ СТОРОН", true, 24));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("ИСПОЛНИТЕЛЬ:                                  ЗАКАЗЧИК:", true, 22));
            body.Append(CreateParagraph($"ООО «Провайдер-Сервис»                        {companyName}", false, 20));
            body.Append(CreateParagraph($"ИНН 6900000000 / КПП 690001001                ИНН {inn} / КПП {kpp}", false, 20));
            body.Append(CreateParagraph($"г. Тверь, ул. Тверская, д. 1                  {legalAddress}", false, 20));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("_____________/Петров П.П.                     _____________/" + director, false, 22));
            body.Append(CreateParagraph("М.П.                                           М.П.", false, 22));
        }

        private static Paragraph CreateParagraph(string text, bool bold = false, int fontSize = 22, bool centered = false)
        {
            var run = new Run();
            var runProps = new RunProperties();

            if (bold)
                runProps.Append(new Bold());

            runProps.Append(new FontSize { Val = fontSize.ToString() });
            runProps.Append(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });

            run.Append(runProps);
            run.Append(new Text(text));

            var para = new Paragraph();
            var paraProps = new ParagraphProperties();

            paraProps.Append(new Justification
            {
                Val = centered ? JustificationValues.Center : JustificationValues.Both
            });

            para.Append(paraProps);
            para.Append(run);

            return para;
        }
        public static void GenerateAct(string filePath, string actNumber, string clientName, string workType, string address, 
            string date, string notes)
        {
            using var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();
            mainPart.Document.Append(body);

            string city = "г. Тверь";
            if (!string.IsNullOrWhiteSpace(address))
            {
                int commaIndex = address.IndexOf(',');
                if (commaIndex > 0)
                    city = address.Substring(0, commaIndex).Trim();
                else
                    city = address.Trim();
            }

            body.Append(CreateParagraph("АКТ № " + actNumber, true, 28, true));
            body.Append(CreateParagraph("выполненных работ", false, 24, true));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph(city + "                                          " + DateTime.Now.ToString("dd MMMM yyyy") + " г.", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("Мы, нижеподписавшиеся,", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("Исполнитель: ПАО «Ростелеком» в лице бригадира, действующего на основании наряда,", false, 22));
            body.Append(CreateParagraph("Заказчик: " + clientName + ",", false, 22));
            body.Append(CreateParagraph("составили настоящий акт о нижеследующем:", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("1. ВЫПОЛНЕННЫЕ РАБОТЫ", true, 24));
            body.Append(CreateParagraph("1.1. Исполнитель выполнил следующие работы: " + workType + ".", false, 22));
            body.Append(CreateParagraph("1.2. Адрес выполнения: " + address + ".", false, 22));
            body.Append(CreateParagraph("1.3. Дата выполнения: " + date + ".", false, 22));
            if (!string.IsNullOrWhiteSpace(notes))
                body.Append(CreateParagraph("1.4. Примечания: " + notes + ".", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("2. ЗАКЛЮЧЕНИЕ", true, 24));
            body.Append(CreateParagraph("2.1. Работы выполнены в полном объёме и с надлежащим качеством.", false, 22));
            body.Append(CreateParagraph("2.2. Заказчик претензий к качеству и срокам выполнения работ не имеет.", false, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("ИСПОЛНИТЕЛЬ:                                  ЗАКАЗЧИК:", true, 22));
            body.Append(CreateParagraph(""));
            body.Append(CreateParagraph("_____________/ПАО «Ростелеком»                _____________/" + clientName, false, 22));
            body.Append(CreateParagraph("М.П.                                           М.П.", false, 22));
        }
    }
}