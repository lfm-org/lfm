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

const DEFAULT_SUBTITLES = [
  "What's our tank doing?!",
  "Pre-potion usage f(x) = 1/x",
  "How long has that Rogue been offline?",
  "Let's Twitch again, like we did last summer",
];

export class Logo extends React.Component<ILogoProps> {
  public static defaultProps = {
    alt: "",
    size: "32px",
    subtitles: DEFAULT_SUBTITLES,
  };

  public render() {
    const subtitle =
      this.props.subtitles[
        Math.floor(Math.random() * this.props.subtitles.length)
      ];
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
        <Typography variant="subtitle2" className="subtitle">
          {subtitle}
        </Typography>
      </div>
    );
  }
}
