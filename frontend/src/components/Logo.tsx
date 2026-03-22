import React from "react";
import { Box, Link, Typography } from "@mui/material";
import { Link as RouterLink } from "react-router";

export interface ILogoProps {
  title: string;
  subtitles?: string[];
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
    subtitles: DEFAULT_SUBTITLES,
  };

  constructor(props: ILogoProps) {
    super(props);
    this.state = { subtitle: (props.subtitles ?? DEFAULT_SUBTITLES)[0] };
  }

  public componentDidMount() {
    const subtitles = this.props.subtitles ?? DEFAULT_SUBTITLES;
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
          <Typography
            variant="h6"
            sx={{ mr: { xs: 2, md: 4 }, whiteSpace: "nowrap" }}
          >
            {this.props.title}
          </Typography>
        </Link>
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
