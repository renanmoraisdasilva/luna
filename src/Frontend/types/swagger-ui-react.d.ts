declare module 'swagger-ui-react' {
  import type { ComponentType } from 'react';

  type SwaggerSpecUrl = {
    name: string;
    url: string;
  };

  type SwaggerUIProps = {
    url?: string;
    urls?: SwaggerSpecUrl[];
    deepLinking?: boolean;
    displayRequestDuration?: boolean;
  };

  const SwaggerUI: ComponentType<SwaggerUIProps>;
  export default SwaggerUI;
}