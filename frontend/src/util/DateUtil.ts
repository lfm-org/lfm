import moment from "moment";

export class DateUtils {
    public static readonly DefaultDateFormat: string = "DD.MM.YYYY HH.mm";

    public static FormatDate(date: string, format: string = DateUtils.DefaultDateFormat) {
        return moment(date).local().format(format);
    }

    public static FormatDateWithPassed(date: string, format: string = DateUtils.DefaultDateFormat) {
        if(moment().local() > moment(date).local()) {
            return "Passed";
        }
        return this.FormatDate(date, format);
    }
}
