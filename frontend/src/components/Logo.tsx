import React from "react";
import "./Logo.css";
import { Typography, Link } from "@material-ui/core";

// eslint:disable-next-line:no-empty-interface
export interface ILogoProps {
  image: string;
  alt: string;
  size: string | number;
  title: string;
  subtitles: string[];
}

// eslint:disable-next-line:no-empty-interface
export interface ILogoState {}

export class Logo extends React.Component<ILogoProps, ILogoState> {
  public static defaultProps = {
    alt: "",
    size: "32pix",
    subtitles: [
      "What's our tank doing?!",
      "Pre-potion usage f(x) = 1/x",
      "How long has that Rogue been offline?",
      "Let's Twitch again, like we did last summer",
    ],
  };

  constructor(props: Readonly<ILogoProps>) {
    super(props);

    this.state = {};
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
        <Typography variant="subtitle2" className="subtitle">
          {
            this.props.subtitles[
              Math.floor(Math.random() * this.props.subtitles.length)
            ]
          }
        </Typography>
      </div>
    );
  }
}
