import { createMuiTheme } from "@material-ui/core/styles";

// A custom theme for this app
const theme = createMuiTheme({
    overrides: {
        MuiToolbar: {
            root: {
                backgroundColor: "#093170",
            },
        },
    },
    palette: {
        type: "dark",
    },
});

export default theme;
