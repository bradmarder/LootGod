// use explicit ".js" version because it does not contain inline-svgs that conflict with CSP policy
import Swal from 'sweetalert2/dist/sweetalert2.js';
import 'sweetalert2/themes/bootstrap-5.css';

export default Swal.mixin({
    theme: 'bootstrap-5',
});