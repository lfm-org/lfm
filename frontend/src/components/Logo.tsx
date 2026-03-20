import React from "react";
import { Box, Link, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";

export interface ILogoProps {
  image: string;
  alt: string;
  size: string | number;
  title: string;
  subtitles: string[];
}

export const DEFAULT_SUBTITLES = [
  "What's our tank doing?!",
  "Pre-potion usage f(x) = 1/x",
  "How long has that Rogue been offline?",
  "Let's Twitch again, like we did last summer",
];

interface ILogoState {
  subtitle: string;
}

export class Logo extends React.Component<ILogoProps, ILogoState> {
  public static defaultProps = {
    alt: "",
    size: "32px",
    subtitles: DEFAULT_SUBTITLES,
  };

  constructor(props: ILogoProps) {
    super(props);
    this.state = { subtitle: props.subtitles[0] };
  }

  public componentDidMount() {
    const { subtitles } = this.props;
    this.setState({
      subtitle: subtitles[Math.floor(Math.random() * subtitles.length)],
    });
  }

  public render() {
    return (
      <Box sx={{ display: "flex", alignItems: "center", flexGrow: 1, minWidth: 0 }}>
        <Link
          component={RouterLink}
          to="/"
          underline="none"
          color="inherit"
          sx={{ display: "inline-flex", alignItems: "center", flexShrink: 0 }}
        >
          <Box
            component="img"
            src={this.props.image}
            alt={this.props.alt}
            sx={{
              width: this.props.size,
              height: this.props.size,
              display: "block",
            }}
          />
        </Link>
        <Typography
          variant="h6"
          sx={{ ml: 1.5, mr: { xs: 2, md: 4 }, whiteSpace: "nowrap" }}
        >
          {this.props.title}
        </Typography>
        <Typography
          variant="subtitle2"
          data-testid="logo-subtitle"
          sx={{
            minWidth: 0,
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
            color: "text.secondary",
          }}
        >
          {this.state.subtitle}
        </Typography>
      </Box>
    );
  }
}
