import React from "react";
import "./Logo.css";
import { Typography, Link } from "@mui/material";

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
      <div className="Logo">
        <Link href="/">
          <img
            src={this.props.image}
            alt={this.props.alt}
            width={this.props.size}
            height={this.props.size}
          />
        </Link>
        <Typography variant="h6" className="title">
          {this.props.title}
        </Typography>
        <Typography
          variant="subtitle2"
          className="subtitle"
          data-testid="logo-subtitle"
        >
          {this.state.subtitle}
        </Typography>
      </div>
    );
  }
}
