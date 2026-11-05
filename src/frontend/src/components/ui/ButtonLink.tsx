import { Link, type LinkProps } from 'react-router'
import { buttonClassName, type ButtonSize, type ButtonVariant } from './buttonStyles'

export interface ButtonLinkProps extends LinkProps {
  variant?: ButtonVariant
  size?: ButtonSize
}

/** A real link (keyboard, middle-click, right-click work) that looks like a Button. */
export function ButtonLink({ variant = 'primary', size = 'md', className, ...rest }: ButtonLinkProps) {
  return <Link className={buttonClassName(variant, size, className)} {...rest} />
}
