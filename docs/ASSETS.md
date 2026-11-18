# Visual assets

Brisa's identity and interface are maintained in the project. Application icons are loaded from Windows at runtime rather than bundled as standalone app-icon resources.

`src/Brisa.WinUI/Assets/OpenAI.svg` identifies OpenAI. Source: https://commons.wikimedia.org/wiki/File:OpenAI_logo_2025_(symbol).svg . The trademark belongs to OpenAI and identifies the connected service only; see https://openai.com/brand/ . It does not imply affiliation and is not relicensed under MIT.

Preserve the dependency texts in `Assets/Licenses`. Documentation screenshots must use fictitious data and exclude personal names, paths and real account content.

`docs/images` contains actual Brisa screenshots with sample data. Third-party app icons and service symbols visible in those screenshots identify their respective products; they are not distributed as independent icon assets and do not imply affiliation.

## Brisa application icon

The approved dock-and-three-modules direction is the product icon. Its original AI-assisted design is in `docs/design/app-icon-dock.png`. The production master is `src/Brisa.WinUI/Assets/Branding/AppIcon.png`, normalized to 1024 pixels with balanced transparent padding. `Assets/AppIcon.ico` contains 16, 20, 24, 28, 32, 40, 48, 64, 96, 128 and 256-pixel RGBA frames exported from that master.

The WinUI executable, window/panel icons, notification-area icon and setup executable share this ICO. The ICO is also copied to build and publish outputs for native runtime loading. These Brisa-owned image assets are distributed under the project's MIT license; third-party service marks retain their separate terms above.
